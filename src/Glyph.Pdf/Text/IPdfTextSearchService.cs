namespace Glyph.Pdf.Text;

/// <summary>
/// Offline PDF text-layer search. Implementations must stay behind this
/// abstraction so Glyph.App / Glyph.Core never depend on a specific extractor
/// (PdfPig today; replaceable later).
/// </summary>
public interface IPdfTextSearchService
{
    Task<PdfSearchResult> SearchAsync(
        string path,
        string query,
        PdfSearchOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Prefetch page text into an in-memory index so later Find calls avoid re-opening
    /// the PDF. Safe to call concurrently; no-ops if already warm for <paramref name="path"/>.
    /// </summary>
    Task WarmIndexAsync(string path, CancellationToken cancellationToken = default);
}
