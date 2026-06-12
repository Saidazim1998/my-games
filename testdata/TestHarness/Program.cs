// Launcher xizmat qatlamining end-to-end testi (UI'siz).
// Ishga tushirish: dotnet run --project testdata\TestHarness
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Launcher;
using Launcher.Models;
using Launcher.Services;

string repoRoot = args.Length > 0 ? args[0] : @"E:\UnityProjects\GameLauncher";
int passed = 0, failed = 0;

void Check(string name, bool ok)
{
    if (ok) { passed++; Console.WriteLine($"  PASS  {name}"); }
    else { failed++; Console.WriteLine($"! FAIL  {name}"); }
}

// Toza boshlash — harness o'z bin papkasida ishlaydi (AppPaths.Root)
try { if (Directory.Exists(AppPaths.GamesDir)) Directory.Delete(AppPaths.GamesDir, true); } catch { }
try { if (File.Exists(AppPaths.InstalledFile)) File.Delete(AppPaths.InstalledFile); } catch { }
InstallService.CleanTemp();

var progress = new Progress<(long done, long? total)>(_ => { });
var db = InstalledDb.Load();

// --- 1. Katalog o'qish (lokal fayl rejimi)
string catalogPath = Path.Combine(repoRoot, "testdata", "games.local.json");
var catalog = await CatalogService.FetchAsync(catalogPath);
Check("katalog o'qildi, 1 ta o'yin", catalog.Games.Count == 1);
var game = catalog.Games[0];

// --- 2. O'rnatish
await InstallService.InstallAsync(game, db, progress, _ => { }, CancellationToken.None);
string exeInstalled = Path.Combine(AppPaths.GameDir("dummy"), "DummyGame.exe");
Check("Games\\dummy\\DummyGame.exe mavjud", File.Exists(exeInstalled));
Check("installed.json = 1.0.0", InstalledDb.Load().Get("dummy") == "1.0.0");
Check("temp tozalangan", !Directory.Exists(AppPaths.GameTempDir("dummy")));
Check("debug papka zip'ga kirmagan",
    !Directory.Exists(Path.Combine(AppPaths.GameDir("dummy"), "DummyGame_BurstDebugInformation_DoNotShip")));

// --- 3. Versiya solishtirish chekkalari
Check("'1.0' vs '1.0.0' — soxta update YO'Q", !VersionUtil.IsNewer("1.0", "1.0.0"));
Check("'1.0.1' > '1.0.0' — update BOR", VersionUtil.IsNewer("1.0.1", "1.0.0"));
Check("'1.10.0' > '1.9.0' — to'g'ri raqamli solishtirish", VersionUtil.IsNewer("1.10.0", "1.9.0"));
Check("'1.0-beta' vs '1.0-beta' — string fallback, update yo'q", !VersionUtil.IsNewer("1.0-beta", "1.0-beta"));
Check("o'rnatilmagan — har doim yangi", VersionUtil.IsNewer("0.1", null));

// --- 4. Buzuq hash: xato + eski nusxa butun qolishi
var badHash = new CatalogGame
{
    Id = game.Id, Name = game.Name, Version = "9.9.9",
    ZipUrl = game.ZipUrl, Sha256 = new string('0', 64), Exe = game.Exe,
};
bool hashThrew = false;
try { await InstallService.InstallAsync(badHash, db, progress, _ => { }, CancellationToken.None); }
catch (InvalidOperationException) { hashThrew = true; }
Check("buzuq sha256 — o'rnatish rad etildi", hashThrew);
Check("eski nusxa tegmagan (exe joyida)", File.Exists(exeInstalled));
Check("versiya hali ham 1.0.0", InstalledDb.Load().Get("dummy") == "1.0.0");

// --- 5. Yangilash 1.0.1 (xuddi shu zip, yangi versiya raqami)
string zip101 = Path.Combine(repoRoot, "releases", "dummy-1.0.1.zip");
File.Copy(game.ZipUrl, zip101, overwrite: true);
var update = new CatalogGame
{
    Id = "dummy", Name = game.Name, Version = "1.0.1",
    ZipUrl = zip101, Sha256 = game.Sha256, Exe = game.Exe,
};
await InstallService.InstallAsync(update, db, progress, _ => { }, CancellationToken.None);
Check("1.0.1 ga yangilandi", InstalledDb.Load().Get("dummy") == "1.0.1");

// --- 6. Qulflangan fayl: IOException + o'yin joyida qolishi
using (File.OpenRead(exeInstalled))
{
    bool ioThrew = false;
    try { await InstallService.InstallAsync(update, db, progress, _ => { }, CancellationToken.None); }
    catch (IOException) { ioThrew = true; }
    Check("fayl band — IOException", ioThrew);
    Check("o'yin joyida (rollback)", File.Exists(exeInstalled));
}

// --- 7. Ishga tushirish
InstallService.Launch(update);
await Task.Delay(2500);
Check("o'yin ishga tushdi (launched.txt)",
    File.Exists(Path.Combine(AppPaths.GameDir("dummy"), "launched.txt")));
foreach (var p in Process.GetProcessesByName("DummyGame")) { try { p.Kill(); } catch { } }

// --- 8. O'chirish
await Task.Delay(500); // process to'liq yopilishini kutamiz
InstallService.Uninstall("dummy", db);
Check("Games\\dummy o'chdi", !Directory.Exists(AppPaths.GameDir("dummy")));
Check("installed.json yozuvi o'chdi", InstalledDb.Load().Get("dummy") == null);

// --- 9. Buzuq katalog — exception (UI buni status sifatida ko'rsatadi, crash emas)
string badCatalog = Path.Combine(AppPaths.Root, "bad.json");
File.WriteAllText(badCatalog, "{ bu json emas ");
bool catThrew = false;
try { await CatalogService.FetchAsync(badCatalog); } catch { catThrew = true; }
Check("buzuq katalog — exception", catThrew);

// --- 10. Xavfsizlik: yomon exe yo'lli yozuvlar filtrlanadi
string evilCatalog = Path.Combine(AppPaths.Root, "evil.json");
File.WriteAllText(evilCatalog, @"{ ""games"": [
  { ""id"": ""evil1"", ""version"": ""1"", ""zipUrl"": ""x"", ""sha256"": ""x"", ""exe"": ""..\\..\\Windows\\System32\\notepad.exe"" },
  { ""id"": ""evil2"", ""version"": ""1"", ""zipUrl"": ""x"", ""sha256"": ""x"", ""exe"": ""C:\\Windows\\System32\\notepad.exe"" },
  { ""id"": ""ok-game"", ""version"": ""1"", ""zipUrl"": ""x"", ""sha256"": ""x"", ""exe"": ""Game.exe"" }
] }");
var filtered = await CatalogService.FetchAsync(evilCatalog);
Check("'..' va rooted exe yozuvlari filtrlandi", filtered.Games.Count == 1 && filtered.Games[0].Id == "ok-game");

Console.WriteLine();
Console.WriteLine($"NATIJA: {passed} PASS, {failed} FAIL");
return failed > 0 ? 1 : 0;
