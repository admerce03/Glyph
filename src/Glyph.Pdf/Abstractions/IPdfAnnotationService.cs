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

    Task<PdfAnnotationInfo> AddStickyNoteAsync(
        IPdfDocument document,
        int pageIndex,
        double xPoints,
        double yPoints,
        string contents,
        PdfAnnotationColor color,
        CancellationToken cancellationToken = default);

    Task<PdfAnnotationInfo> AddInkAsync(
        IPdfDocument document,
        int pageIndex,
        IReadOnlyList<PdfPagePoint> strokePoints,
        PdfAnnotationColor color,
        float borderWidthPoints = 2f,
        CancellationToken cancellationToken = default);

    Task<PdfAnnotationInfo> AddShapeAsync(
        IPdfDocument document,
        int pageIndex,
        PdfShapeKind kind,
        PdfRect bounds,
        PdfAnnotationColor borderColor,
        PdfAnnotationColor? fillColor = null,
        float borderWidthPoints = 1.5f,
        CancellationToken cancellationToken = default);

    Task<PdfAnnotationInfo> AddTextBoxAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRect bounds,
        string contents,
        PdfAnnotationColor textColor,
        PdfAnnotationColor? borderColor = null,
        float fontSizePoints = 12f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert a stamp annotation backed by a BGRA32 image (e.g. signature PNG with alpha).
    /// </summary>
    Task<PdfAnnotationInfo> AddStampAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRect bounds,
        ReadOnlyMemory<byte> bgraPixels,
        int pixelWidth,
        int pixelHeight,
        CancellationToken cancellationToken = default);

    Task SetContentsAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        string contents,
        CancellationToken cancellationToken = default);

    Task SetColorAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        PdfAnnotationColor color,
        CancellationToken cancellationToken = default);

    Task MoveAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        PdfRect bounds,
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

    /// <summary>
    /// Bake annotations into page content. When <paramref name="pageIndexes"/> is null, all pages are flattened.
    /// </summary>
    Task<PdfFlattenResult> FlattenAsync(
        IPdfDocument document,
        IReadOnlyList<int>? pageIndexes = null,
        bool forPrint = false,
        CancellationToken cancellationToken = default);
}
