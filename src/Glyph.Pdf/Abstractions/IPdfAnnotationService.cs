namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Create, list, and remove PDF annotations (text markup first).
/// </summary>
public interface IPdfAnnotationService
{
    Task<PdfAnnotationInfo> AddTextMarkupAsync(
        IPdfDocument document,
        int pageIndex,
        PdfTextMarkupKind kind,
        IReadOnlyList<PdfQuad> quads,
        PdfAnnotationColor color,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PdfAnnotationInfo>> ListAsync(
        IPdfDocument document,
        int? pageIndex = null,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        CancellationToken cancellationToken = default);
}
