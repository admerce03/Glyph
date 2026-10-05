namespace Glyph.Core.Documents;

/// <summary>
/// Required AutomationProperties.Name values on MainWindow chrome (F02-24 / F56-01).
/// </summary>
public static class ChromeAutomationNames
{
    public const string Sidebar = "Sidebar";
    public const string ResizeSidebar = "Resize sidebar";
    public const string OpenDocuments = "Open documents";
    public const string Preferences = "Preferences";
    public const string Paste = "Paste";
    public const string VersionSnapshots = "Version snapshots";
    public const string CheckForUpdates = "Check for updates";
    public const string AboutGlyph = "About Glyph";

    public static IReadOnlyList<string> RequiredNames { get; } =
    [
        Sidebar,
        ResizeSidebar,
        OpenDocuments,
        Preferences,
        Paste,
        VersionSnapshots,
        CheckForUpdates,
        AboutGlyph,
    ];
}
