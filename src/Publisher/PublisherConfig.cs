using System.Text.Json;

namespace Publisher;

/// <summary>Publisher dasturi sozlamalari — exe yonidagi publisher-config.json da saqlanadi.</summary>
public class PublisherConfig
{
    public string RepoRoot { get; set; } = "";
    public string GhPath { get; set; } = "";

    private static string ConfigFile =>
        Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory,
            "publisher-config.json");

    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public static PublisherConfig Load()
    {
        PublisherConfig cfg;
        try
        {
            cfg = File.Exists(ConfigFile)
                ? JsonSerializer.Deserialize<PublisherConfig>(File.ReadAllText(ConfigFile)) ?? new()
                : new();
        }
        catch { cfg = new(); }

        if (string.IsNullOrWhiteSpace(cfg.RepoRoot)) cfg.RepoRoot = GuessRepoRoot();
        if (string.IsNullOrWhiteSpace(cfg.GhPath)) cfg.GhPath = GuessGhPath();
        return cfg;
    }

    public void Save()
    {
        try { File.WriteAllText(ConfigFile, JsonSerializer.Serialize(this, Opt)); } catch { }
    }

    // games.json bor papkani yuqoriga qarab qidiradi (loyiha ichidan ishga tushganda)
    private static string GuessRepoRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "games.json"))) return dir.FullName;
            dir = dir.Parent;
        }
        return @"E:\UnityProjects\GameLauncher"; // ma'lum standart joy
    }

    private static string GuessGhPath()
    {
        string p = @"C:\Program Files\GitHub CLI\gh.exe";
        return File.Exists(p) ? p : "gh"; // PATH'dagi gh
    }
}
