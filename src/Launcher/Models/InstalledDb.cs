using System.Text.Json;

namespace Launcher.Models;

/// <summary>installed.json: o'yin id → o'rnatilgan versiya. Atomik saqlanadi.</summary>
public class InstalledDb
{
    private readonly Dictionary<string, string> _versions = new();

    public string? Get(string id) => _versions.TryGetValue(id, out var v) ? v : null;

    public void Set(string id, string version)
    {
        _versions[id] = version;
        Save();
    }

    public void Remove(string id)
    {
        _versions.Remove(id);
        Save();
    }

    public static InstalledDb Load()
    {
        var db = new InstalledDb();
        try
        {
            if (File.Exists(AppPaths.InstalledFile))
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(AppPaths.InstalledFile), Json.Options);
                if (dict != null)
                    foreach (var kv in dict) db._versions[kv.Key] = kv.Value;
            }
        }
        catch
        {
            // buzuq fayl — bo'sh db bilan davom etamiz, crash yo'q
        }
        return db;
    }

    private void Save()
    {
        string tmp = AppPaths.InstalledFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_versions, Json.Options));
        File.Move(tmp, AppPaths.InstalledFile, overwrite: true);
    }
}
