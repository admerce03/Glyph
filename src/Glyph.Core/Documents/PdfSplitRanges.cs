namespace Glyph.Core.Documents;

/// <summary>
/// Pure helpers for computing contiguous page ranges when splitting a PDF.
/// </summary>
public static class PdfSplitRanges
{
    /// <summary>
    /// Returns contiguous zero-based page index ranges. Each value in
    /// <paramref name="splitBeforeIndexes"/> starts a new document (index 0 is implied).
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<int>> BuildRanges(int pageCount, IReadOnlyList<int> splitBeforeIndexes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        ArgumentNullException.ThrowIfNull(splitBeforeIndexes);
        if (pageCount == 0)
        {
            return [];
        }

        var starts = splitBeforeIndexes
            .Where(i => i > 0 && i < pageCount)
            .Distinct()
            .OrderBy(i => i)
            .Prepend(0)
            .ToList();

        var ranges = new List<IReadOnlyList<int>>(starts.Count);
        for (var s = 0; s < starts.Count; s++)
        {
            var begin = starts[s];
            var endExclusive = s + 1 < starts.Count ? starts[s + 1] : pageCount;
            ranges.Add(Enumerable.Range(begin, endExclusive - begin).ToArray());
        }

        return ranges;
    }
}
