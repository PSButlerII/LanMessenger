using LanMessenger.Hubs;
using LanMessenger.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using System.Net;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddSingleton<MessageStore>();
builder.Services.AddSingleton<DeviceAuthService>();
//builder.Services.AddAuthentication();

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

var app = builder.Build();

//app.UseForwardedHeaders(new ForwardedHeadersOptions
//{
//    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
//});
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



// Bind to all interfaces so other LAN machines can reach it.
// You can also do this via launchSettings.json or command line.


app.Run();
