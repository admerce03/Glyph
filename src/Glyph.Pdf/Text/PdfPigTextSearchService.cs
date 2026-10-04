using UglyToad.PdfPig;

namespace Glyph.Pdf.Text;

/// <summary>
/// Offline full-text search over PDF text layers using PdfPig.
/// Does not require rasterizing pages.
/// </summary>
public sealed class PdfPigTextSearchService : IPdfTextSearchService
{
    public Task<IReadOnlyList<PdfSearchHit>> SearchAsync(
        string path,
        string query,
        bool caseSensitive = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                var hits = new List<PdfSearchHit>();

                using var document = PdfDocument.Open(path);
                for (var pageIndex = 0; pageIndex < document.NumberOfPages; pageIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var page = document.GetPage(pageIndex + 1);
                    var text = page.Text ?? string.Empty;
                    var searchFrom = 0;
                    while (searchFrom < text.Length)
                    {
                        var found = text.IndexOf(query, searchFrom, comparison);
                        if (found < 0)
                        {
                            break;
                        }

                        hits.Add(new PdfSearchHit(
                            PageIndex: pageIndex,
                            Snippet: BuildSnippet(text, found, query.Length),
                            MatchStart: found,
                            MatchLength: query.Length));

                        searchFrom = found + Math.Max(1, query.Length);
                    }
                }

                return (IReadOnlyList<PdfSearchHit>)hits;
            },
            cancellationToken);
    }

    private static string BuildSnippet(string text, int matchStart, int matchLength)
    {
        const int pad = 28;
        var start = Math.Max(0, matchStart - pad);
        var end = Math.Min(text.Length, matchStart + matchLength + pad);
        var snippet = text[start..end].Replace('\n', ' ').Replace('\r', ' ');
        if (start > 0)
        {
            snippet = "…" + snippet;
        }

        if (end < text.Length)
        {
            snippet += "…";
        }

        return snippet.Trim();
    }
}
