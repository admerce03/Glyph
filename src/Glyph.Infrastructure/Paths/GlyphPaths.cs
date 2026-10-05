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

    public static string SessionFile => System.IO.Path.Combine(LocalAppDataRoot, "session.json");

    public static string RecoveryDirectory => System.IO.Path.Combine(LocalAppDataRoot, "recovery");

    public static string SnapshotsDirectory => System.IO.Path.Combine(LocalAppDataRoot, "snapshots");

    public static string SignaturesDirectory => System.IO.Path.Combine(LocalAppDataRoot, "signatures");

    public static string FormValueHistoryFile => System.IO.Path.Combine(LocalAppDataRoot, "form-values.json");

    public static string FormAutofillProfileFile => System.IO.Path.Combine(LocalAppDataRoot, "form-profile.json");

    public static string TempDirectory => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Glyph");
}
