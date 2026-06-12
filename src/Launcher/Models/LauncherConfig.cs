using System.Text.Json;

namespace Launcher.Models;

public class LauncherConfig
{
    public string CatalogUrl { get; set; } =
        "https://raw.githubusercontent.com/Saidazim1998/my-games/main/games.json";

    public static LauncherConfig Load()
    {
        try
        {
            if (File.Exists(AppPaths.ConfigFile))
                return JsonSerializer.Deserialize<LauncherConfig>(
                    File.ReadAllText(AppPaths.ConfigFile), Json.Options) ?? new LauncherConfig();
        }
        catch
        {
            // buzuq config — default bilan davom etamiz, ustidan yozmaymiz
            return new LauncherConfig();
        }
        var cfg = new LauncherConfig();
        cfg.Save();
        return cfg;
    }

    public void Save() =>
        File.WriteAllText(AppPaths.ConfigFile, JsonSerializer.Serialize(this, Json.Options));
}
