namespace Glyph.Core.Documents;

/// <summary>
/// Annotation multi-select toggle semantics (F13-27).
/// </summary>
public static class AnnotationMultiSelectPolicy
{
    public static string StatusAfterToggle(int selectedCount) =>
        selectedCount <= 1
            ? (selectedCount == 1 ? "Annotation selected." : "Selection cleared.")
            : $"Selected {selectedCount} annotations (Ctrl+click to toggle).";

    public static string StatusAfterSelect(int selectedCount) =>
        selectedCount == 1
            ? "Annotation selected."
            : $"Selected {selectedCount} annotations.";
}

/// <summary>
/// Persistent highlight mode toggle (F14-02).
/// </summary>
public static class PersistentHighlightMode
{
    public static bool ExitOnEscape(bool currentlyActive) => currentlyActive;

    public static bool Toggle(bool currentlyActive) => !currentlyActive;
}

/// <summary>
/// Main window content grid uses a * star column for the document area (F02-08).
/// </summary>
public static class DocumentAreaLayout
{
    public const string ContentGridColumns = "Auto,6,*";

    public static bool DeclaresStarDocumentColumn(string mainWindowXaml) =>
        !string.IsNullOrWhiteSpace(mainWindowXaml)
        && mainWindowXaml.Contains($"ColumnDefinitions=\"{ContentGridColumns}\"", StringComparison.Ordinal);
}

/// <summary>
/// Shell open entry points (F01-01).
/// </summary>
public static class OpenEntryPoints
{
    public static IReadOnlyList<string> Catalog { get; } =
    [
        "File → Open",
        "File → Open Multiple",
        "Drag and drop",
        "Recent files",
    ];
}
