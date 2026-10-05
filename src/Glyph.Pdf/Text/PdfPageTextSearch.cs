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
                    BuildSnippet(text, index, trimmed.Length),
                    index,
                    trimmed.Length));
                start = index + Math.Max(1, trimmed.Length);
            }
        }

        return hits;
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
