namespace Glyph.Core.Documents;

/// <summary>
/// Persist/restore of Find Match-case / Any-word / sort prefs (F06-03 / F06-05 / F06-12).
/// Shared by the PDF toolbar and Edit → Find in all open PDFs.
/// </summary>
public static class FindOptionsPolicy
{
    public const int SortPageOrderIndex = 0;
    public const int SortRelevanceIndex = 1;

    public static int SortComboIndex(bool sortByRelevance) =>
        sortByRelevance ? SortRelevanceIndex : SortPageOrderIndex;

    public static bool SortByRelevanceFromIndex(int selectedIndex) =>
        selectedIndex == SortRelevanceIndex;

    public static void ApplyTo(
        bool caseSensitive,
        bool anyWord,
        bool sortByRelevance,
        out bool caseOut,
        out bool anyOut,
        out int sortIndex)
    {
        caseOut = caseSensitive;
        anyOut = anyWord;
        sortIndex = SortComboIndex(sortByRelevance);
    }
}
