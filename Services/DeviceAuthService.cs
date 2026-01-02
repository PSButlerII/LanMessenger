using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LanMessenger.Services;

public class DeviceAuthService
{
    private readonly string _dbPath;
    private readonly object _lock = new();
    private DeviceDb _db = new();

    public DeviceAuthService(IWebHostEnvironment env, IConfiguration cfg)
    {
        _dbPath =
           cfg["Storage:DeviceDbPath"]
           ?? Path.Combine(env.ContentRootPath, "App_Data", "devices.json");

        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);

        if (File.Exists(_dbPath))
        {
            var json = File.ReadAllText(_dbPath);
            _db = JsonSerializer.Deserialize<DeviceDb>(json) ?? new DeviceDb();
        }
        else
        {
            Save();
        }
    }
    

    public bool Validate(string deviceId, string deviceKey)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(deviceKey))
            return false;

        lock (_lock)
        {
            var record = _db.Devices.FirstOrDefault(d =>
                string.Equals(d.DeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase));

            if (record is null) return false;

            var keyHash = Sha256(deviceKey.Trim());
            var stored = Convert.FromBase64String(record.KeyHashBase64);

            return CryptographicOperations.FixedTimeEquals(keyHash, stored);
        }
    }

    public void AddOrReplace(string deviceId, string deviceKey)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(deviceKey))
            throw new ArgumentException("deviceId/deviceKey required.");

        lock (_lock)
        {
            var hash = Convert.ToBase64String(Sha256(deviceKey.Trim()));
            _db.Devices.RemoveAll(d => string.Equals(d.DeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase));
            _db.Devices.Add(new DeviceRecord { DeviceId = deviceId.Trim(), KeyHashBase64 = hash });
            Save();
        }
    }

    public bool Revoke(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("deviceId required.");

        lock (_lock)
        {
            var before = _db.Devices.Count;
            _db.Devices.RemoveAll(d => string.Equals(d.DeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase));
            var changed = _db.Devices.Count != before;
            if (changed) Save();
            return changed;
        }
    }


    public IReadOnlyList<string> ListDeviceIds()
    {
        lock (_lock)
        {
            return _db.Devices.Select(d => d.DeviceId).OrderBy(x => x).ToList();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_db, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_dbPath, json);
    }

    private static byte[] Sha256(string value)
        => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private class DeviceDb
    {
        public List<DeviceRecord> Devices { get; set; } = new();
    }

    private class DeviceRecord
    {
        public string DeviceId { get; set; } = "";
        public string KeyHashBase64 { get; set; } = "";
    }
}
