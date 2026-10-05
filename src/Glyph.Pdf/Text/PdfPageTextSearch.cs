namespace Glyph.Pdf.Text;

/// <summary>
/// Finds query matches in per-page plain text (native extract or session OCR cache).
/// </summary>
public static class PdfPageTextSearch
{
    public static IReadOnlyList<PdfSearchHit> Find(
        IReadOnlyDictionary<int, string> pageTexts,
        string query,
        bool caseSensitive = false)
    {
        if (pageTexts.Count == 0 || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var trimmed = query.Trim();
        var comparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var hits = new List<PdfSearchHit>();

        foreach (var pageIndex in pageTexts.Keys.OrderBy(i => i))
        {
            var text = pageTexts[pageIndex];
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var start = 0;
            while (start < text.Length)
            {
                var index = text.IndexOf(trimmed, start, comparison);
                if (index < 0)
                {
                    break;
                }

                hits.Add(new PdfSearchHit(
                    pageIndex,
                    PdfSearchSnippet.Build(text, index, trimmed.Length),
                    index,
                    trimmed.Length));
                start = index + Math.Max(1, trimmed.Length);
            }
        }

        return hits;
    }

    /// <summary>
    /// Merges native extract hits with OCR-cache hits, ordered by page/offset and
    /// deduped on (page, start, length) so overlapping OCR/native matches collapse.
    /// </summary>
    public static IReadOnlyList<PdfSearchHit> Merge(
        IReadOnlyList<PdfSearchHit> nativeHits,
        IReadOnlyList<PdfSearchHit> ocrHits)
    {
        ArgumentNullException.ThrowIfNull(nativeHits);
        ArgumentNullException.ThrowIfNull(ocrHits);

        if (ocrHits.Count == 0)
        {
            return nativeHits;
        }

        if (nativeHits.Count == 0)
        {
            return ocrHits;
        }

        var seen = new HashSet<(int Page, int Start, int Length)>();
        var merged = new List<PdfSearchHit>(nativeHits.Count + ocrHits.Count);
        foreach (var hit in nativeHits.Concat(ocrHits).OrderBy(h => h.PageIndex).ThenBy(h => h.MatchStart))
        {
            var key = (hit.PageIndex, hit.MatchStart, hit.MatchLength);
            if (!seen.Add(key))
            {
                continue;
            }

            merged.Add(hit);
        }

        return merged;
    }
}
