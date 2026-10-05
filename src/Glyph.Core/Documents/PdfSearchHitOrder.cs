namespace Glyph.Core.Documents;

/// <summary>
/// Find-results ordering (F06-12): page order / document scan order.
/// Relevance sort is deferred.
/// </summary>
public static class PdfSearchHitOrder
{
    public static IReadOnlyList<T> ByPageThenOccurrence<T>(
        IEnumerable<T> hits,
        Func<T, int> pageIndex,
        Func<T, int>? occurrenceIndex = null)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(pageIndex);

        if (occurrenceIndex is null)
        {
            return hits.OrderBy(pageIndex).ToArray();
        }

        return hits.OrderBy(pageIndex).ThenBy(occurrenceIndex).ToArray();
    }
}
