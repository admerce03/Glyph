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
        string? author = null,
        CancellationToken cancellationToken = default);

    Task<PdfAnnotationInfo> AddInkAsync(
        IPdfDocument document,
        int pageIndex,
        IReadOnlyList<PdfPagePoint> strokePoints,
        PdfAnnotationColor color,
        float borderWidthPoints = 2f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closed freeform ink path (first point appended at end when needed).
    /// </summary>
    Task<PdfAnnotationInfo> AddFreeformAsync(
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
        PdfAnnotationColor? fillColor = null,
        float fontSizePoints = 12f,
        string fontResourceName = "Helv",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// FreeText callout: text box plus an ink pointer from <paramref name="tip"/> to the box.
    /// </summary>
    Task<PdfAnnotationInfo> AddCalloutAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRect textBounds,
        PdfPagePoint tip,
        string contents,
        PdfAnnotationColor textColor,
        PdfAnnotationColor? borderColor = null,
        PdfAnnotationColor? fillColor = null,
        float fontSizePoints = 12f,
        string fontResourceName = "Helv",
        float pointerWidthPoints = 1.5f,
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

    /// <summary>
    /// Set annotation opacity (0–1), preserving RGB from the current stroke color when available.
    /// </summary>
    Task SetOpacityAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        float opacity,
        CancellationToken cancellationToken = default);

    Task MoveAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        PdfRect bounds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Duplicate an annotation on the same page, offset slightly from the original.
    /// Stamp annotations are not supported yet (pixel payload is not retained in the list model).
    /// </summary>
    Task<PdfAnnotationInfo> DuplicateAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
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
