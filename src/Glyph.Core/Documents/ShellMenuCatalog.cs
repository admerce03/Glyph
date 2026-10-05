namespace Glyph.Core.Documents;

/// <summary>
/// Top-level MenuBar titles and shell commands (F02-02).
/// Values must stay aligned with MainWindow.xaml.
/// </summary>
public static class ShellMenuCatalog
{
    public static IReadOnlyList<string> TopLevelMenus { get; } =
        ["File", "Edit", "View", "Window", "Help"];

    public static IReadOnlyList<string> FileCommands { get; } =
    [
        "Open…",
        "Open Multiple…",
        "New from Clipboard",
        "Capture from Camera…",
        "Scan…",
        "Save",
        "Save As…",
        "Duplicate",
        "Rename…",
        "Move…",
        "Share…",
        "Show in File Explorer",
        "Properties",
        "Version Snapshots…",
        "Copy File Path",
        "Copy File",
        "Open With Default App",
        "Send Email…",
        "New Window",
        "Close Tab",
        "Close All",
        "Clear Recent Files",
        "Exit",
    ];

    public static IReadOnlyList<string> EditCommands { get; } =
    [
        "Paste",
        "Find in all open PDFs…",
    ];

    public static IReadOnlyList<string> ViewCommands { get; } =
    [
        "Hide Sidebar",
        "Hide Toolbar",
        "Full Screen",
        "System",
        "Light",
        "Dark",
        "Preferences…",
    ];

    public static IReadOnlyList<string> WindowCommands { get; } =
    [
        "Next Tab",
        "Previous Tab",
        "Move Tab to New Window",
        "Move to Next Monitor",
    ];

    public static IReadOnlyList<string> HelpCommands { get; } =
    [
        "Check for Updates…",
        "About Glyph",
    ];

    public static bool DeclaresMenus(string mainWindowXaml) =>
        TopLevelMenus.All(title =>
            mainWindowXaml.Contains($"MenuBarItem Title=\"{title}\"", StringComparison.Ordinal));

    public static bool DeclaresCommandTexts(
        string mainWindowXaml,
        IEnumerable<string> commands) =>
        commands.All(command =>
            mainWindowXaml.Contains($"Text=\"{command}\"", StringComparison.Ordinal));
}
