namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Read/write PDF annotations behind the document engine.
/// </summary>
public interface IPdfAnnotationStore
{
    Task<IReadOnlyList<PdfAnnotation>> ListAsync(
        IPdfDocument document,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PdfAnnotation>> ListPageAsync(
        IPdfDocument document,
        int pageIndex,
        CancellationToken cancellationToken = default);

    Task<PdfAnnotation> AddTextMarkupAsync(
        IPdfDocument document,
        PdfTextMarkupRequest request,
        CancellationToken cancellationToken = default);

    Task<PdfAnnotation> AddStickyNoteAsync(
        IPdfDocument document,
        PdfStickyNoteRequest request,
        CancellationToken cancellationToken = default);

    Task<PdfAnnotation> AddShapeAsync(
        IPdfDocument document,
        PdfShapeRequest request,
        CancellationToken cancellationToken = default);

    Task<PdfAnnotation> AddInkAsync(
        IPdfDocument document,
        PdfInkRequest request,
        CancellationToken cancellationToken = default);

    Task<PdfAnnotation> AddFreeTextAsync(
        IPdfDocument document,
        PdfFreeTextRequest request,
        CancellationToken cancellationToken = default);

    Task SetColorAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        PdfAnnotationColor color,
        CancellationToken cancellationToken = default);

    Task SetContentsAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        string contents,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        CancellationToken cancellationToken = default);
}
