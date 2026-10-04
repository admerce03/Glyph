namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Renders individual pages on demand. Callers are responsible for caching/eviction.
/// </summary>
public interface IPdfRenderer
{
    Task<PdfRenderResult> RenderPageAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRenderRequest request,
        CancellationToken cancellationToken = default);
}
