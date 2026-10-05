namespace Glyph.Core.Documents;

/// <summary>
/// Top-level MenuBar titles and primary File/Edit/View/Window commands (F02-02).
/// Values must stay aligned with MainWindow.xaml.
/// </summary>
public static class ShellMenuCatalog
{
    public static IReadOnlyList<string> TopLevelMenus { get; } =
        ["File", "Edit", "View", "Window"];

    public static IReadOnlyList<string> FileCommands { get; } =
    [
        "Open…",
        "Open Multiple…",
        "New from Clipboard",
        "Save",
        "Save As…",
        "Duplicate",
        "Properties",
        "New Window",
        "Close Tab",
        "Exit",
    ];

    public static bool DeclaresMenus(string mainWindowXaml) =>
        TopLevelMenus.All(title =>
            mainWindowXaml.Contains($"MenuBarItem Title=\"{title}\"", StringComparison.Ordinal));
}
