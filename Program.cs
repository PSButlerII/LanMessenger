using LanMessenger.Hubs;
using LanMessenger.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using System.Net;
using System.Security.Cryptography;
using LanMessenger.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddSingleton<MessageStore>();
builder.Services.AddSingleton<DeviceAuthService>();
//builder.Services.AddAuthentication();
builder.Services.AddHostedService<RetentionService>();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 1024L * 1024L * 1024L; // 1 GB
});

//builder.Services.ConfigureApplicationCookie(o =>
//{
//    o.ExpireTimeSpan = TimeSpan.FromDays(5);
//    o.SlidingExpiration = true;
//});

//builder.Services.ConfigureApplicationCookie(options =>
//{
//    options.Cookie.HttpOnly = true;
//    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
//});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();



app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
//app.UseHttpsRedirection();

app.UseStaticFiles();
var uploadsPath = app.Configuration["Storage:UploadsPath"]
    ?? Path.Combine(app.Environment.ContentRootPath, "App_Data", "uploads");

if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/files"
});

app.MapRazorPages();
app.MapHub<ChatHub>("/chatHub");

app.MapGet("/admin/devices", (HttpRequest request, DeviceAuthService auth) =>
{
    var cfg = app.Configuration;

    var sec = cfg.GetSection("Security");
    if (sec.GetValue<bool>("AdminLanOnly"))
    {
        var ip = request.HttpContext.Connection.RemoteIpAddress;
        if (ip is null || !IsPrivateIp(ip))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (!AdminAuthorized(request, cfg))
        return Results.StatusCode(StatusCodes.Status401Unauthorized);

    return Results.Ok(new { devices = auth.ListDeviceIds() });
});

app.MapPost("/admin/devices", async (HttpRequest request, DeviceAuthService auth) =>
{
    var cfg = app.Configuration;

    var sec = cfg.GetSection("Security");
    if (sec.GetValue<bool>("AdminLanOnly"))
    {
        var ip = request.HttpContext.Connection.RemoteIpAddress;
        if (ip is null || !IsPrivateIp(ip))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (!AdminAuthorized(request, cfg))
        return Results.StatusCode(StatusCodes.Status401Unauthorized);

    // Accept either JSON or form
    string? action = null;
    string? deviceId = null;
    string? deviceKey = null;

    if (request.HasJsonContentType())
    {
        var body = await request.ReadFromJsonAsync<AdminDeviceRequest>();
        action = body?.Action;
        deviceId = body?.DeviceId;
        deviceKey = body?.DeviceKey;
    }
    else
    {
        var form = await request.ReadFormAsync();
        action = form["action"].ToString();
        deviceId = form["deviceId"].ToString();
        deviceKey = form["deviceKey"].ToString();
    }

    action = (action ?? "").Trim().ToLowerInvariant();
    deviceId = (deviceId ?? "").Trim();

    if (action is "add" or "replace")
    {
        deviceKey = (deviceKey ?? "").Trim();
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(deviceKey))
            return Results.BadRequest("deviceId and deviceKey are required for add/replace.");

        auth.AddOrReplace(deviceId, deviceKey);
        return Results.Ok(new { ok = true, action = "add", deviceId });
    }

    if (action is "revoke" or "delete" or "remove")
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return Results.BadRequest("deviceId is required for revoke.");

        var changed = auth.Revoke(deviceId);
        return Results.Ok(new { ok = true, action = "revoke", deviceId, removed = changed });
    }

    return Results.BadRequest("action must be add|replace|revoke");
});

app.MapPost("/upload", async (
    HttpRequest request,
    IHubContext<ChatHub> hub,
    DeviceAuthService auth) =>
{
    var cfg = app.Configuration.GetSection("Security");

    if (!cfg.GetValue<bool>("UploadsEnabled"))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    // LAN-only gate (toggleable)
    var lanOnly = cfg.GetValue<bool>("LanOnlyUploads");
    var remoteIp = request.HttpContext.Connection.RemoteIpAddress;

    if (lanOnly && (remoteIp is null || !IsPrivateIp(remoteIp)))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    // Per-device auth gate
    var requireDeviceAuth = cfg.GetValue<bool>("RequireDeviceAuthForUploads");
    if (requireDeviceAuth)
    {
        var deviceId = request.Headers["X-Device-Id"].ToString();
        var deviceKey = request.Headers["X-Device-Key"].ToString();

        if (!auth.Validate(deviceId, deviceKey))
            return Results.StatusCode(StatusCodes.Status401Unauthorized);
    }

    // Read the form ONCE (after auth gates)
    var form = await request.ReadFormAsync();
    var file = form.Files["file"];
    var sender = form["sender"].ToString();

    if (file == null || file.Length == 0)
        return Results.BadRequest("No file uploaded.");

    const long maxBytes = 1024L * 1024L * 1024L; // 1GB
    if (file.Length > maxBytes)
        return Results.BadRequest("File too large.");

    var originalName = CleanName(Path.GetFileName(file.FileName));

    var stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
    var safeName = $"{stamp}__{originalName}";
    var savePath = Path.Combine(uploadsPath, safeName);

    await using (var fs = File.Create(savePath))
        await file.CopyToAsync(fs);

    var payload = new
    {
        timestamp = DateTimeOffset.Now.ToString("HH:mm:ss"),
        sender = string.IsNullOrWhiteSpace(sender) ? "Unknown" : sender,
        fileName = originalName,
        url = $"/files/{Uri.EscapeDataString(safeName)}",
        size = file.Length
    };

    await hub.Clients.All.SendAsync("fileUploaded", payload);
    return Results.Ok(payload);
});


static string CleanName(string name)
{
    foreach (var c in Path.GetInvalidFileNameChars())
        name = name.Replace(c, '_');
    return name;
}

static bool IsPrivateIp(IPAddress ip)
{
    if (IPAddress.IsLoopback(ip)) return true;

    var bytes = ip.GetAddressBytes();
    // IPv4 private ranges:
    // 10.0.0.0/8
    if (bytes[0] == 10) return true;
    // 172.16.0.0/12
    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
    // 192.168.0.0/16
    if (bytes[0] == 192 && bytes[1] == 168) return true;

    return false;
}

static bool AdminAuthorized(HttpRequest request, IConfiguration cfg)
{
    var sec = cfg.GetSection("Security");
    if (!sec.GetValue<bool>("AdminEnabled"))
        return false;

    var adminKey = sec.GetValue<string>("AdminKey") ?? "";
    if (string.IsNullOrWhiteSpace(adminKey))
        return false;

    // header preferred
    var provided = request.Headers["X-Admin-Key"].ToString();
    if (string.IsNullOrWhiteSpace(provided))
        return false;

    return FixedTimeEqualsString(provided, adminKey);
}
static bool FixedTimeEqualsString(string a, string b)
{
    var aBytes = System.Text.Encoding.UTF8.GetBytes(a ?? "");
    var bBytes = System.Text.Encoding.UTF8.GetBytes(b ?? "");
    if (aBytes.Length != bBytes.Length) return false;
    return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
}



// Bind to all interfaces so other LAN machines can reach it.
// You can also do this via launchSettings.json or command line.


app.Run();
