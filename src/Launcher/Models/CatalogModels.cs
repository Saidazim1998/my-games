namespace Launcher.Models;

public class Catalog
{
    public List<CatalogGame> Games { get; set; } = new();
}

public class CatalogGame
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string ZipUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    /// <summary>O'yin papkasi ichidagi nisbiy exe yo'li (masalan "MyGame.exe").</summary>
    public string Exe { get; set; } = "";
    public string? CoverUrl { get; set; }
    public string? Changelog { get; set; }
}
