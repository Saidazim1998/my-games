using System.Text.Json;
using Launcher.Models;

namespace Launcher.Services;

public static class CatalogService
{
    /// <summary>
    /// games.json ni yuklaydi. URL http bilan boshlanmasa lokal fayl deb o'qiydi
    /// (GitHub repo ochilgunga qadar test rejimi).
    /// </summary>
    public static async Task<Catalog> FetchAsync(string catalogUrl)
    {
        string json;
        if (catalogUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            json = await Http.Client.GetStringAsync(catalogUrl, cts.Token);
        }
        else
        {
            json = await File.ReadAllTextAsync(catalogUrl);
        }

        var catalog = JsonSerializer.Deserialize<Catalog>(json, Json.Options) ?? new Catalog();
        catalog.Games.RemoveAll(g => !IsValid(g));
        return catalog;
    }

    // Buzilgan katalog launcherni C:\Windows\... kabi yo'llarni ishga tushirishga
    // majburlay olmasligi kerak — exe nisbiy va Games\<id> ichida bo'lishi shart
    private static bool IsValid(CatalogGame g) =>
        !string.IsNullOrWhiteSpace(g.Id) &&
        !string.IsNullOrWhiteSpace(g.Version) &&
        !string.IsNullOrWhiteSpace(g.ZipUrl) &&
        !string.IsNullOrWhiteSpace(g.Exe) &&
        !g.Exe.Contains("..") &&
        !Path.IsPathRooted(g.Exe) &&
        g.Id.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
