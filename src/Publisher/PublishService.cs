using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Publisher;

public class GameInput
{
    public string BuildDir = "";
    public string GameId = "";
    public string GameName = "";
    public string Version = "";
    public string ExeName = "";
    public string Changelog = "";
    public string Description = "";
    public string CoverFile = ""; // lokal rasm yo'li (ixtiyoriy)
}

/// <summary>
/// Release jarayonini boshqaradi: zip → SHA-256 → games.json → gh release → git push.
/// Hamma qadam log() orqali xabar beradi.
/// </summary>
public class PublishService
{
    private readonly string _repoRoot;
    private readonly string _ghPath;

    public PublishService(string repoRoot, string ghPath)
    {
        _repoRoot = repoRoot;
        _ghPath = ghPath;
    }

    // Unity ship qilmaydigan papkalar
    private static readonly string[] ExcludeDirs =
    {
        "_BurstDebugInformation_DoNotShip",
        "_BackUpThisFolder_ButDontShipItWithYourGame",
    };

    public async Task<bool> ReleaseAsync(GameInput g, bool publishToGitHub, Action<string> log, CancellationToken ct)
    {
        try
        {
            // 0. Tekshiruvlar
            if (!Directory.Exists(g.BuildDir)) { log("❌ Build papkasi topilmadi: " + g.BuildDir); return false; }
            string exePath = Path.Combine(g.BuildDir, g.ExeName);
            if (!File.Exists(exePath)) { log("❌ Exe topilmadi: " + exePath); return false; }
            if (string.IsNullOrWhiteSpace(g.GameId) || string.IsNullOrWhiteSpace(g.Version))
            { log("❌ O'yin ID va versiya bo'sh bo'lmasligi kerak."); return false; }
            if (!File.Exists(Path.Combine(_repoRoot, "games.json")))
            { log("❌ games.json topilmadi. Sozlamalarda repo papkasini to'g'rilang: " + _repoRoot); return false; }

            // 1. Owner/Repo ni git remote'dan aniqlaymiz
            string remote = await RunCapture("git", "remote get-url origin", ct);
            var (owner, repo) = ParseOwnerRepo(remote);
            if (owner == null) { log("❌ GitHub remote aniqlanmadi. 'git remote -v' tekshiring."); return false; }
            log($"📦 Repo: {owner}/{repo}");

            // 2. Zip (build ICHIDAGILARI arxiv ildizida)
            string releasesDir = Path.Combine(_repoRoot, "releases");
            Directory.CreateDirectory(releasesDir);
            string zipName = $"{g.GameId}-{g.Version}.zip";
            string zipPath = Path.Combine(releasesDir, zipName);
            log("🗜  Zip yaratilmoqda (keraksiz papkalar chiqarib tashlanmoqda)...");
            await Task.Run(() => CreateZip(g.BuildDir, zipPath), ct);
            log($"   {zipName} — {Mb(zipPath)} MB");

            // 3. SHA-256
            log("🔒 SHA-256 hisoblanmoqda...");
            string hash = await Sha256Async(zipPath, ct);

            // 4. Muqova (ixtiyoriy)
            string? coverUrl = null;
            if (!string.IsNullOrWhiteSpace(g.CoverFile) && File.Exists(g.CoverFile))
            {
                string coversDir = Path.Combine(_repoRoot, "covers");
                Directory.CreateDirectory(coversDir);
                string ext = Path.GetExtension(g.CoverFile);
                if (string.IsNullOrEmpty(ext)) ext = ".png";
                File.Copy(g.CoverFile, Path.Combine(coversDir, g.GameId + ext), overwrite: true);
                coverUrl = $"https://raw.githubusercontent.com/{owner}/{repo}/main/covers/{g.GameId}{ext}";
                log("🖼  Muqova nusxalandi.");
            }

            // 5. games.json yangilash (id bo'yicha almashtir yoki qo'sh)
            string gamesJson = Path.Combine(_repoRoot, "games.json");
            var catalog = CatalogIo.Load(gamesJson);
            var entry = catalog.Games.FirstOrDefault(x => x.Id == g.GameId);
            bool isNew = entry == null;
            if (entry == null) { entry = new CatalogGame(); catalog.Games.Add(entry); }
            string tag = $"{g.GameId}-v{g.Version}";

            entry.Id = g.GameId;
            entry.Name = g.GameName;
            entry.Version = g.Version;
            entry.ZipUrl = $"https://github.com/{owner}/{repo}/releases/download/{tag}/{zipName}";
            entry.Sha256 = hash;
            entry.Exe = g.ExeName;
            entry.Changelog = g.Changelog;
            if (coverUrl != null) entry.CoverUrl = coverUrl;             // berilmasa eskisi qoladi
            if (!string.IsNullOrWhiteSpace(g.Description))
                entry.Description = g.Description;                       // berilmasa eskisi qoladi
            CatalogIo.Save(gamesJson, catalog);
            log($"📝 games.json yangilandi ({(isNew ? "yangi o'yin qo'shildi" : "versiya yangilandi")}).");

            if (!publishToGitHub)
            {
                log("");
                log("✅ Lokal tayyor (GitHub'ga yuborilmadi).");
                log("   Zip: " + zipPath);
                return true;
            }

            // 6. git add + commit
            var toAdd = new List<string> { "games.json" };
            if (coverUrl != null) toAdd.Add("covers");
            await Run("git", "add " + string.Join(" ", toAdd), log, ct);
            await Run("git", $"commit -m \"{g.GameId} {g.Version}\"", log, ct, allowFail: true); // "nothing to commit" bo'lishi mumkin

            // 7. gh release create (zip yuklash) — tag mavjud bo'lsa xato beradi
            log("☁  GitHub Release yaratilmoqda, zip yuklanmoqda (kattaroq o'yinlarda biroz vaqt oladi)...");
            await Run(_ghPath,
                $"release create {tag} \"{zipPath}\" --title \"{g.GameName} {g.Version}\" --notes \"{g.Changelog}\" --repo {owner}/{repo}",
                log, ct);

            // 8. git push
            await Run("git", "push", log, ct);

            log("");
            log("✅ TAYYOR! ~5 daqiqada foydalanuvchilarning launcherida ko'rinadi.");
            return true;
        }
        catch (OperationCanceledException) { log("⏹ Bekor qilindi."); return false; }
        catch (Exception ex) { log("❌ XATO: " + ex.Message); return false; }
    }

    // ---- yordamchi ----

    private void CreateZip(string buildDir, string zipPath)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        string full = Path.GetFullPath(buildDir);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var file in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(full, file);
            string top = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            if (ExcludeDirs.Any(suffix => top.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
                continue;
            zip.CreateEntryFromFile(file, rel.Replace('\\', '/'), CompressionLevel.Optimal);
        }
    }

    private static async Task<string> Sha256Async(string file, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var fs = File.OpenRead(file);
        return Convert.ToHexString(await sha.ComputeHashAsync(fs, ct));
    }

    private static string Mb(string file) => Math.Round(new FileInfo(file).Length / 1024.0 / 1024.0, 1).ToString();

    private static (string? owner, string? repo) ParseOwnerRepo(string remoteUrl)
    {
        remoteUrl = remoteUrl.Trim();
        if (remoteUrl.EndsWith(".git")) remoteUrl = remoteUrl[..^4];
        // https://github.com/Owner/Repo  yoki  git@github.com:Owner/Repo
        int idx = remoteUrl.IndexOf("github.com", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return (null, null);
        string tail = remoteUrl[(idx + "github.com".Length)..].TrimStart(':', '/');
        var parts = tail.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? (parts[0], parts[1]) : (null, null);
    }

    private async Task Run(string exe, string args, Action<string> log, CancellationToken ct, bool allowFail = false)
    {
        log($"$ {Path.GetFileNameWithoutExtension(exe)} {args}");
        var (code, output) = await RunRaw(exe, args, ct);
        foreach (var line in output.Split('\n'))
            if (!string.IsNullOrWhiteSpace(line)) log("   " + line.TrimEnd());
        if (code != 0 && !allowFail)
            throw new Exception($"'{Path.GetFileNameWithoutExtension(exe)} {args}' xato kodi {code} bilan tugadi.");
    }

    private async Task<string> RunCapture(string exe, string args, CancellationToken ct)
    {
        var (_, output) = await RunRaw(exe, args, ct);
        return output;
    }

    private async Task<(int code, string output)> RunRaw(string exe, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        using var proc = new Process { StartInfo = psi };
        var sb = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(ct);
        return (proc.ExitCode, sb.ToString());
    }
}
