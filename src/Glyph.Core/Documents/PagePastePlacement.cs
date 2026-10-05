namespace Glyph.Core.Documents;

/// <summary>
/// Insert index when pasting copied PDF pages (F10-24).
/// </summary>
public static class PagePastePlacement
{
    /// <summary>
    /// Insert after the last selected thumbnail, or after the current page when none selected.
    /// </summary>
    public static int InsertAfterSelection(
        IReadOnlyList<int> selectedPageIndexes,
        int currentPageIndex,
        int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        ArgumentNullException.ThrowIfNull(selectedPageIndexes);
        var anchor = selectedPageIndexes.Count > 0
            ? selectedPageIndexes.Max()
            : currentPageIndex;
        return PageInsertIndex.Clamp(anchor + 1, pageCount);
    }
}
