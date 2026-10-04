namespace Glyph.Infrastructure.Paths;

/// <summary>
/// Standard on-disk locations for Glyph preferences and recovery data.
/// </summary>
public static class GlyphPaths
{
    public static string LocalAppDataRoot =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Glyph");

    public static string SettingsFile => System.IO.Path.Combine(LocalAppDataRoot, "settings.json");

    public static string RecentFilesFile => System.IO.Path.Combine(LocalAppDataRoot, "recent.json");

    public static string DocumentViewStateFile => System.IO.Path.Combine(LocalAppDataRoot, "view-state.json");

    public static string RecoveryDirectory => System.IO.Path.Combine(LocalAppDataRoot, "recovery");

    public static string SignaturesDirectory => System.IO.Path.Combine(LocalAppDataRoot, "signatures");

    public static string TempDirectory => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Glyph");
}
