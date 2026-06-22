// PublishService'ning lokal (GitHub'siz) mantiqini tekshiradi: zip + hash + games.json.
// git/gh chaqirilmaydi (publishToGitHub=false), shuning uchun real release yaratilmaydi.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Publisher;

string repoRoot = args.Length > 0 ? args[0] : @"E:\UnityProjects\GameLauncher";
string buildDir = Path.Combine(repoRoot, "testdata", "DummyGame", "publish");
int passed = 0, failed = 0;
void Check(string name, bool ok)
{
    if (ok) { passed++; Console.WriteLine($"  PASS  {name}"); }
    else { failed++; Console.WriteLine($"! FAIL  {name}"); }
}

// Izolyatsiya qilingan vaqtinchalik "repo" — asl games.json ga tegmaymiz
string sandbox = Path.Combine(Path.GetTempPath(), "pub-test-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(sandbox);
File.WriteAllText(Path.Combine(sandbox, "games.json"), "{ \"games\": [] }");

// Owner/Repo aniqlanishi uchun sandbox'da soxta git remote
static void Git(string dir, string a)
{
    var p = Process.Start(new ProcessStartInfo("git", a) { WorkingDirectory = dir, UseShellExecute = false })!;
    p.WaitForExit();
}
Git(sandbox, "init");
Git(sandbox, "remote add origin https://github.com/TestOwner/test-repo.git");

var svc = new PublishService(sandbox, "gh");
var log = new List<string>();
void L(string s) => log.Add(s);

// --- 1. Yangi o'yin qo'shish (lokal rejim)
var input = new GameInput
{
    BuildDir = buildDir,
    GameId = "racing-game",
    GameName = "Racing Game",
    Version = "2.0.0",
    ExeName = "DummyGame.exe",
    Changelog = "Test changelog",
    Description = "Test tavsif",
};
bool ok = await svc.ReleaseAsync(input, publishToGitHub: false, L, CancellationToken.None);
Check("ReleaseAsync (lokal) muvaffaqiyatli", ok);

string zip = Path.Combine(sandbox, "releases", "racing-game-2.0.0.zip");
Check("zip yaratildi", File.Exists(zip));

// Zip ichida exe root'da, DoNotShip yo'q
using (var z = ZipFile.OpenRead(zip))
{
    var names = z.Entries.Select(e => e.FullName).ToList();
    Check("zip ildizida DummyGame.exe bor", names.Contains("DummyGame.exe"));
    Check("DoNotShip papka zip'da yo'q", !names.Any(n => n.Contains("DoNotShip")));
}

// games.json yangilandi, hash to'g'ri
var cat = JsonSerializer.Deserialize<Catalog>(
    File.ReadAllText(Path.Combine(sandbox, "games.json")),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
Check("katalogda 1 ta o'yin", cat.Games.Count == 1);
var g0 = cat.Games[0];
Check("versiya 2.0.0", g0.Version == "2.0.0");
Check("exe yozildi", g0.Exe == "DummyGame.exe");
Check("description yozildi", g0.Description == "Test tavsif");

string expectHash;
using (var sha = System.Security.Cryptography.SHA256.Create())
using (var fs = File.OpenRead(zip))
    expectHash = Convert.ToHexString(sha.ComputeHash(fs));
Check("sha256 katalogdagi bilan mos", string.Equals(g0.Sha256, expectHash, StringComparison.OrdinalIgnoreCase));
Check("zipUrl tag konvensiyasiga mos", g0.ZipUrl.EndsWith("racing-game-v2.0.0/racing-game-2.0.0.zip"));
Check("zipUrl owner/repo to'g'ri", g0.ZipUrl.Contains("TestOwner/test-repo"));

// --- 2. Mavjud o'yinni yangilash — description bo'sh bo'lsa eskisi qoladi
var upd = new GameInput
{
    BuildDir = buildDir, GameId = "racing-game", GameName = "Racing Game",
    Version = "2.0.1", ExeName = "DummyGame.exe", Changelog = "v2.0.1",
    Description = "", // bo'sh
};
await svc.ReleaseAsync(upd, publishToGitHub: false, L, CancellationToken.None);
cat = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(Path.Combine(sandbox, "games.json")),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
Check("hali ham 1 ta o'yin (almashtirildi, qo'shilmadi)", cat.Games.Count == 1);
Check("versiya 2.0.1 ga yangilandi", cat.Games[0].Version == "2.0.1");
Check("bo'sh description -> eski tavsif qoldi", cat.Games[0].Description == "Test tavsif");

// --- 3. Ikkinchi o'yin qo'shish
var second = new GameInput
{
    BuildDir = buildDir, GameId = "welding-sim", GameName = "Welding Sim",
    Version = "1.0.0", ExeName = "DummyGame.exe", Changelog = "Birinchi",
    Description = "Svarka",
};
await svc.ReleaseAsync(second, publishToGitHub: false, L, CancellationToken.None);
cat = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(Path.Combine(sandbox, "games.json")),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
Check("endi 2 ta o'yin", cat.Games.Count == 2);

// --- 4. Yo'q exe -> xato, false qaytaradi
var bad = new GameInput
{
    BuildDir = buildDir, GameId = "x", GameName = "X", Version = "1.0.0",
    ExeName = "YoqExe.exe", Changelog = "",
};
bool badOk = await svc.ReleaseAsync(bad, publishToGitHub: false, L, CancellationToken.None);
Check("yo'q exe -> false", !badOk);

Directory.Delete(sandbox, true);
Console.WriteLine();
Console.WriteLine($"NATIJA: {passed} PASS, {failed} FAIL");
return failed > 0 ? 1 : 0;
