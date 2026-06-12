namespace Launcher;

public static class VersionUtil
{
    /// <summary>
    /// Katalogdagi versiya o'rnatilganidan yangimi. Hech qachon throw qilmaydi.
    /// </summary>
    public static bool IsNewer(string remote, string? installed)
    {
        if (string.IsNullOrWhiteSpace(installed)) return true;
        if (Version.TryParse(remote, out var r) && Version.TryParse(installed, out var i))
            return Normalize(r) > Normalize(i);
        // "1.0-beta" kabi parse bo'lmaydigan versiyalar: farq bor = yangilanish bor
        return !string.Equals(remote, installed, StringComparison.Ordinal);
    }

    // "1.0" vs "1.0.0": yetishmagan komponentlar -1 bo'lib soxta update ko'rsatmasligi uchun
    private static Version Normalize(Version v) => new(
        Math.Max(v.Major, 0), Math.Max(v.Minor, 0),
        Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
}
