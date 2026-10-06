namespace LanMessenger.Services;

public class RetentionService : BackgroundService
{
    private readonly ILogger<RetentionService> _logger;
    private readonly IConfiguration _cfg;
    private readonly IWebHostEnvironment _env;

    public RetentionService(ILogger<RetentionService> logger, IConfiguration cfg, IWebHostEnvironment env)
    {
        _logger = logger;
        _cfg = cfg;
        _env = env;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { RunCleanup(); }
            catch (Exception ex) { _logger.LogError(ex, "Retention cleanup failed."); }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private void RunCleanup()
    {
        if (!_cfg.GetValue<bool>("Retention:Enabled"))
            return;

        var uploadsPath =
            _cfg["Storage:UploadsPath"]
            ?? Path.Combine(_env.ContentRootPath, "App_Data", "uploads");

        if (!Directory.Exists(uploadsPath)) return;

        var maxAgeDays = _cfg.GetValue<int>("Retention:MaxFileAgeDays");
        var maxTotalMb = _cfg.GetValue<int>("Retention:MaxTotalSizeMB");

        var files = new DirectoryInfo(uploadsPath)
            .GetFiles("*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToList();

        // Delete older than N days
        if (maxAgeDays > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);
            foreach (var f in files.Where(x => x.LastWriteTimeUtc < cutoff).ToList())
            {
                TryDelete(f);
                files.Remove(f);
            }
        }

        // Cap total size (delete oldest until under cap)
        if (maxTotalMb > 0)
        {
            long capBytes = (long)maxTotalMb * 1024L * 1024L;
            long total = files.Sum(f => f.Length);

            foreach (var f in files.OrderBy(f => f.LastWriteTimeUtc).ToList())
            {
                if (total <= capBytes) break;
                total -= f.Length;
                TryDelete(f);
            }
        }
    }

    private void TryDelete(FileInfo f)
    {
        try
        {
            f.Delete();
            _logger.LogInformation("Deleted old upload: {File}", f.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete {File}", f.FullName);
        }
    }
}
