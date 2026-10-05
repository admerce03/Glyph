namespace Glyph.Core.Documents;

/// <summary>
/// Sticky-note expand/collapse and add/export status strings (F15).
/// </summary>
public static class StickyNoteExpandPolicy
{
    public const string SelectToExpand = "Select a sticky note to expand.";
    public const string NoExpandedNotes = "No expanded sticky notes.";
    public const string AlreadyCollapsed = "Note is already collapsed.";
    public const string CollapsedAll = "Collapsed all sticky notes.";
    public const string NoteCancelled = "Note cancelled.";
    public const string Adding = "Adding note…";
    public const string Added = "Sticky note added.";
    public const string NoNotesToExport = "No sticky notes to export.";
    public const string ExportCancelled = "Export notes cancelled.";
    public const string CannotRotateMarkup =
        "Sticky notes and text markup cannot be rotated.";
    public const string SelectToEdit =
        "Select a sticky note, text box, or callout to edit.";
    public const string EditAppliesToNotes =
        "Edit applies to sticky notes, text boxes, and callouts.";

    public static string Expanded(string label) => $"Expanded {label}.";

    public static string Collapsed(string label) => $"Collapsed {label}.";

    public static bool ShouldCollapseAll(bool hasSelectedSticky, int expandedCount) =>
        !hasSelectedSticky && expandedCount > 0;

    public static string FormatExported(int noteCount, string fileName) =>
        $"Exported {noteCount} note{(noteCount == 1 ? string.Empty : "s")} to {fileName}.";
}
