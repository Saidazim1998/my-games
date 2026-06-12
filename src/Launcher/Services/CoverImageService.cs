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
            string cache = AppPaths.CoverFile(game.Id);
            if (!File.Exists(cache))
            {
                if (string.IsNullOrWhiteSpace(game.CoverUrl)) return Placeholder;
                Directory.CreateDirectory(AppPaths.CoversDir);
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
