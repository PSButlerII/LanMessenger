using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LanMessenger.Services;

public class DeviceAuthService
{
    private readonly string _path;
    private DeviceDb _db = new();

    public DeviceAuthService(IWebHostEnvironment env)
    {
        var appData = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appData);

        _path = Path.Combine(appData, "devices.json");
        if (File.Exists(_path))
        {
            var json = File.ReadAllText(_path);
            _db = JsonSerializer.Deserialize<DeviceDb>(json) ?? new DeviceDb();
        }
        else
        {
            Save(); // create empty file
        }
    }

    public bool Validate(string deviceId, string deviceKey)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(deviceKey))
            return false;

        var record = _db.Devices.FirstOrDefault(d =>
            string.Equals(d.DeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase));

        if (record is null) return false;

        var keyHash = Sha256(deviceKey.Trim());
        var stored = Convert.FromBase64String(record.KeyHashBase64);

        return CryptographicOperations.FixedTimeEquals(keyHash, stored);
    }

    public void AddOrReplace(string deviceId, string deviceKey)
    {
        var hash = Convert.ToBase64String(Sha256(deviceKey));
        _db.Devices.RemoveAll(d => string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        _db.Devices.Add(new DeviceRecord { DeviceId = deviceId, KeyHashBase64 = hash });
        Save();
    }

    public void Revoke(string deviceId)
    {
        _db.Devices.RemoveAll(d => string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_db, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
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
