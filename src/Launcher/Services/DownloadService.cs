using System.Net.Http;
using System.Security.Cryptography;

namespace Launcher.Services;

public static class DownloadService
{
    /// <summary>
    /// Faylni progress bilan stream qilib yuklaydi. ResponseHeadersRead shart —
    /// aks holda HttpClient butun zipni RAMga yig'adi. URL http bilan boshlanmasa
    /// lokal fayl deb nusxalaydi (test rejimi).
    /// </summary>
    public static async Task DownloadAsync(string url, string destFile,
        IProgress<(long done, long? total)> progress, CancellationToken ct)
    {
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Run(() => File.Copy(url, destFile, overwrite: true), ct);
            progress.Report((1, 1));
            return;
        }

        using var resp = await Http.Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        long? total = resp.Content.Headers.ContentLength; // null bo'lishi mumkin -> indeterminate bar

        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(destFile);
        var buffer = new byte[81920];
        long done = 0, lastReport = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            done += read;
            // UI thread dispatcher'ini ko'mib tashlamaslik uchun throttle
            if (done - lastReport > 262_144)
            {
                progress.Report((done, total));
                lastReport = done;
            }
        }
        progress.Report((done, total));
    }

    public static async Task<string> Sha256Async(string file, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var fs = File.OpenRead(file);
        return Convert.ToHexString(await sha.ComputeHashAsync(fs, ct));
    }
}
