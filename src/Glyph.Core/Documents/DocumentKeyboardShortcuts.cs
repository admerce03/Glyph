namespace Glyph.Core.Documents;

/// <summary>
/// Document-level keyboard gestures (F52) beyond shell menu accelerators.
/// </summary>
public static class DocumentKeyboardShortcuts
{
    public static IReadOnlyList<(string Command, string Gesture)> Catalog { get; } =
    [
        ("Print", "Ctrl+P"),
        ("Find", "Ctrl+F"),
        ("Find next", "F3"),
        ("Find previous", "Shift+F3"),
        ("Copy", "Ctrl+C"),
        ("Cut", "Ctrl+X"),
        ("Paste", "Ctrl+V"),
        ("Select all", "Ctrl+A"),
        ("Select all pages", "Ctrl+Shift+A"),
        ("Undo", "Ctrl+Z"),
        ("Redo", "Ctrl+Y"),
        ("Zoom in", "Ctrl++"),
        ("Zoom out", "Ctrl+-"),
        ("Fit / actual size", "Ctrl+0"),
        ("Delete selection", "Delete"),
        ("Previous page", "PageUp"),
        ("Next page", "PageDown"),
    ];

    public static bool CatalogContains(string gesture) =>
        Catalog.Any(c => string.Equals(c.Gesture, gesture, StringComparison.OrdinalIgnoreCase));
}
