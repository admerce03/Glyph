namespace Glyph.Core.Documents;

/// <summary>
/// Sticky-note expand/collapse status strings (F15-03/04).
/// </summary>
public static class StickyNoteExpandPolicy
{
    public const string SelectToExpand = "Select a sticky note to expand.";
    public const string NoExpandedNotes = "No expanded sticky notes.";
    public const string AlreadyCollapsed = "Note is already collapsed.";
    public const string CollapsedAll = "Collapsed all sticky notes.";

    public static string Expanded(string label) => $"Expanded {label}.";

    public static string Collapsed(string label) => $"Collapsed {label}.";

    public static bool ShouldCollapseAll(bool hasSelectedSticky, int expandedCount) =>
        !hasSelectedSticky && expandedCount > 0;
}
