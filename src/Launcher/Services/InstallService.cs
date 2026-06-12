using System.Diagnostics;
using System.IO.Compression;
using Launcher.Models;

namespace Launcher.Services;

public static class InstallService
{
    /// <summary>
    /// Atomik o'rnatish: temp ga yuklash → hash → extract → swap.
    /// Har qanday bosqich yiqilsa o'rnatilgan eski nusxa butun qoladi.
    /// </summary>
    public static async Task InstallAsync(CatalogGame game, InstalledDb db,
        IProgress<(long done, long? total)> progress, Action<string> status, CancellationToken ct)
    {
        string tempDir = AppPaths.GameTempDir(game.Id);
        TryDeleteDir(tempDir);
        Directory.CreateDirectory(tempDir);
        try
        {
            string zipPath = Path.Combine(tempDir, "game.zip");
            status("Yuklab olinmoqda...");
            await DownloadService.DownloadAsync(game.ZipUrl, zipPath, progress, ct);

            status("Fayl tekshirilmoqda...");
            string hash = await DownloadService.Sha256Async(zipPath, ct);
            if (!hash.Equals(game.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Hash mos kelmadi — yuklab olingan fayl buzilgan.");

            status("Arxiv ochilmoqda...");
            string extractDir = Path.Combine(tempDir, "extracted");
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true), ct);
            File.Delete(zipPath);

            ct.ThrowIfCancellationRequested();
            status("O'rnatilmoqda...");
            Directory.CreateDirectory(AppPaths.GamesDir);
            string gameDir = AppPaths.GameDir(game.Id);
            string oldDir = Path.Combine(tempDir, "old");
            bool hadOld = Directory.Exists(gameDir);
            if (hadOld) MoveWithRetry(gameDir, oldDir);
            try
            {
                MoveWithRetry(extractDir, gameDir);
            }
            catch
            {
                if (hadOld) MoveWithRetry(oldDir, gameDir); // rollback
                throw;
            }
            db.Set(game.Id, game.Version);
        }
        finally
        {
            TryDeleteDir(tempDir);
        }
    }

    public static void Uninstall(string id, InstalledDb db)
    {
        string dir = AppPaths.GameDir(id);
        if (Directory.Exists(dir)) DeleteWithRetry(dir);
        db.Remove(id);
    }

    public static void Launch(CatalogGame game)
    {
        string gameDir = Path.GetFullPath(AppPaths.GameDir(game.Id));
        string exePath = Path.GetFullPath(Path.Combine(gameDir, game.Exe));
        if (!exePath.StartsWith(gameDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Katalogdagi exe yo'li noto'g'ri.");
        if (!File.Exists(exePath))
            throw new FileNotFoundException("O'yin exe fayli topilmadi.", exePath);

        // WorkingDirectory muhim — ba'zi Unity buildlar nisbiy yo'llarni CWD dan oladi
        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = gameDir,
            UseShellExecute = true,
        });
    }

    /// <summary>temp\ sof tranzit papka — startupda tozalanadi.</summary>
    public static void CleanTemp() => TryDeleteDir(AppPaths.TempDir);

    // Antivirus yangi extract qilingan fayllarni qisqa muddatga qulflashi mumkin
    private static void MoveWithRetry(string from, string to)
    {
        for (int i = 0; ; i++)
        {
            try { Directory.Move(from, to); return; }
            catch (IOException) when (i < 2) { Thread.Sleep(500); }
        }
    }

    private static void DeleteWithRetry(string dir)
    {
        for (int i = 0; ; i++)
        {
            try { Directory.Delete(dir, recursive: true); return; }
            catch (IOException) when (i < 2) { Thread.Sleep(500); }
        }
    }

    private static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { /* best-effort */ }
    }
}
