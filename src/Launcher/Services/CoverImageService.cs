using System.Windows.Media;
using System.Windows.Media.Imaging;
using Launcher.Models;

namespace Launcher.Services;

public static class CoverImageService
{
    private static ImageSource? _placeholder;
    public static ImageSource Placeholder => _placeholder ??= LoadPack();

    /// <summary>Muqovani keshdan yoki URL dan oladi; har qanday xatoda placeholder.</summary>
    public static async Task<ImageSource> GetAsync(CatalogGame game)
    {
        try
        {
            string cache = AppPaths.CoverFile(game.Id, game.Version);
            if (!File.Exists(cache))
            {
                if (string.IsNullOrWhiteSpace(game.CoverUrl)) return Placeholder;
                Directory.CreateDirectory(AppPaths.CoversDir);
                PurgeOldCovers(game.Id, keep: Path.GetFileName(cache)); // eski versiya keshlarini o'chir
                byte[] bytes;
                if (game.CoverUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    bytes = await Http.Client.GetByteArrayAsync(game.CoverUrl, cts.Token);
                }
                else
                {
                    bytes = await File.ReadAllBytesAsync(game.CoverUrl);
                }
                await File.WriteAllBytesAsync(cache, bytes);
            }
            return await Task.Run(() => LoadFile(cache));
        }
        catch
        {
            return Placeholder;
        }
    }

    // Shu o'yinning eski muqova kesh fayllarini o'chiradi (joriy versiyanikidan tashqari),
    // shu jumladan eski formatdagi <id>.img faylni ham — kesh shishib ketmasligi uchun
    private static void PurgeOldCovers(string id, string keep)
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(AppPaths.CoversDir, id + "__*.img"))
                if (!string.Equals(Path.GetFileName(f), keep, StringComparison.OrdinalIgnoreCase))
                    File.Delete(f);
            string legacy = Path.Combine(AppPaths.CoversDir, id + ".img");
            if (File.Exists(legacy)) File.Delete(legacy);
        }
        catch { /* best-effort */ }
    }

    // OnLoad — fayl handle'ini darhol bo'shatadi; Freeze — boshqa threadda yaratilgan
    // bitmapni UI threadda ishlatish mumkin bo'ladi
    private static ImageSource LoadFile(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private static ImageSource LoadPack()
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri("pack://application:,,,/Assets/placeholder-cover.png");
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
