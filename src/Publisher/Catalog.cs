using System.Text.Json;

namespace Publisher;

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
    public string Exe { get; set; } = "";
    public string? CoverUrl { get; set; }
    public string? Changelog { get; set; }
    public string? Description { get; set; }
}

public static class CatalogIo
{
    private static readonly JsonSerializerOptions Opt = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static Catalog Load(string path)
    {
        if (!File.Exists(path)) return new Catalog();
        try { return JsonSerializer.Deserialize<Catalog>(File.ReadAllText(path), Opt) ?? new Catalog(); }
        catch { return new Catalog(); }
    }

    public static void Save(string path, Catalog catalog) =>
        File.WriteAllText(path, JsonSerializer.Serialize(catalog, Opt));
}
