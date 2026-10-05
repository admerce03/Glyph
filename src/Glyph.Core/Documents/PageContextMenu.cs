namespace Glyph.Core.Documents;

/// <summary>
/// Page / annotation right-click command labels (F60).
/// </summary>
public static class PageContextMenu
{
    public const string Copy = "Copy";
    public const string Highlight = "Highlight";
    public const string Underline = "Underline";
    public const string Strikethrough = "Strikethrough";
    public const string FindSelection = "Find selection";
    public const string SearchWeb = "Search web";
    public const string Style = "Style…";
    public const string Duplicate = "Duplicate";
    public const string Delete = "Delete";
    public const string Edit = "Edit…";
    public const string Align = "Align…";

    public static IReadOnlyList<string> TextSelectionCommands { get; } =
    [
        Copy,
        Highlight,
        Underline,
        Strikethrough,
        FindSelection,
        SearchWeb,
    ];

    public static IReadOnlyList<string> AnnotationCommands { get; } =
    [
        Style,
        Duplicate,
        Edit,
        Copy,
        Delete,
    ];
}
