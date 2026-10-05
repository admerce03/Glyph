namespace Glyph.Core.Documents;

/// <summary>
/// Shell menu keyboard accelerators (F02-23). Values must match MainWindow.xaml.
/// </summary>
public static class ShellKeyboardShortcuts
{
    public static IReadOnlyList<(string Command, string Gesture)> Catalog { get; } =
    [
        ("Open", "Ctrl+O"),
        ("New from Clipboard", "Ctrl+Shift+N"),
        ("Save", "Ctrl+S"),
        ("Save As", "Ctrl+Shift+S"),
        ("Duplicate", "Ctrl+Shift+D"),
        ("Properties", "Ctrl+I"),
        ("New Window", "Ctrl+N"),
        ("Close Tab", "Ctrl+W"),
        ("Exit", "Ctrl+Q"),
        ("Paste", "Ctrl+V"),
        ("Find in all open PDFs", "Ctrl+Shift+F"),
        ("Full Screen", "F11"),
        ("Toggle Sidebar", "Ctrl+Shift+B"),
        ("Toggle Toolbar", "Ctrl+Shift+U"),
        ("Preferences", "Ctrl+OemComma"),
        ("Next Tab", "Ctrl+Tab"),
        ("Previous Tab", "Ctrl+Shift+Tab"),
    ];

    public static bool CatalogContains(string gesture) =>
        Catalog.Any(c => string.Equals(c.Gesture, gesture, StringComparison.OrdinalIgnoreCase));
}
