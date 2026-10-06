namespace Glyph.Pdf.Text;

/// <summary>
/// Finds query matches in per-page plain text (native extract or session OCR cache).
/// </summary>
public static class PdfPageTextSearch
{
    public static IReadOnlyList<PdfSearchHit> Find(
        IReadOnlyDictionary<int, string> pageTexts,
        string query,
        bool caseSensitive = false,
        bool exactPhrase = true)
    {
        if (pageTexts.Count == 0 || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var trimmed = query.Trim();
        var comparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var needles = exactPhrase
            ? new[] { trimmed }
            : trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (needles.Length == 0)
        {
            return [];
        }

        var hits = new List<PdfSearchHit>();

        foreach (var pageIndex in pageTexts.Keys.OrderBy(i => i))
        {
            var text = pageTexts[pageIndex];
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            foreach (var needle in needles)
            {
                CollectHits(hits, pageIndex, text, needle, comparison);
            }
        }

        return hits
            .OrderBy(h => h.PageIndex)
            .ThenBy(h => h.MatchStart)
            .ToArray();
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

    private static void CollectHits(
        List<PdfSearchHit> hits,
        int pageIndex,
        string text,
        string needle,
        StringComparison comparison)
    {
        var start = 0;
        while (start < text.Length)
        {
            var index = text.IndexOf(needle, start, comparison);
            if (index < 0)
            {
                break;
            }

            hits.Add(new PdfSearchHit(
                pageIndex,
                BuildSnippet(text, index, needle.Length),
                index,
                needle.Length));
            start = index + Math.Max(1, needle.Length);
        }
    }

    private static string BuildSnippet(string text, int matchStart, int matchLength)
    {
        const int pad = 28;
        var from = Math.Max(0, matchStart - pad);
        var to = Math.Min(text.Length, matchStart + matchLength + pad);
        var snippet = text[from..to].Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (from > 0)
        {
            snippet = "…" + snippet;
        }

        if (to < text.Length)
        {
            snippet += "…";
        }

        return snippet;
    }
}
