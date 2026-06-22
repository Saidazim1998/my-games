namespace Launcher;

/// <summary>
/// Barcha disk yo'llari bir joyda. Single-file publish'da Assembly.Location bo'sh
/// bo'lgani uchun Environment.ProcessPath ishlatiladi. temp\ exe yonida turishi shart —
/// atomik swap Directory.Move ishlatadi, u disklar orasida ishlamaydi.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } =
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    public static string ConfigFile => Path.Combine(Root, "config.json");
    public static string InstalledFile => Path.Combine(Root, "installed.json");
    public static string GamesDir => Path.Combine(Root, "Games");
    public static string TempDir => Path.Combine(Root, "temp");
    public static string CoversDir => Path.Combine(Root, "covers");

    public static string GameDir(string id) => Path.Combine(GamesDir, id);
    public static string GameTempDir(string id) => Path.Combine(TempDir, id);

    // Muqova keshi versiyaga bog'langan: yangi release (yangi versiya) -> yangi kesh fayli,
    // ya'ni banner o'zgarsa launcher uni qayta yuklab oladi. <id>__<version>.img
    public static string CoverFile(string id, string version) =>
        Path.Combine(CoversDir, $"{id}__{Sanitize(version)}.img");

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }
}
