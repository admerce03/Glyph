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

    /// <summary>
    /// Closed polygon from explicit vertices (click-to-place). Distinct from freehand freeform.
    /// </summary>
    Task<PdfAnnotationInfo> AddPolygonAsync(
        IPdfDocument document,
        int pageIndex,
        IReadOnlyList<PdfPagePoint> vertices,
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
        PdfInkLineStyle inkLineStyle = PdfInkLineStyle.Solid,
        PdfArrowheadStyle arrowheadStyle = PdfArrowheadStyle.Open,
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
        bool underline = false,
        PdfTextQuadding quadding = PdfTextQuadding.Left,
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
        bool underline = false,
        PdfTextQuadding quadding = PdfTextQuadding.Left,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Set FreeText <c>/Q</c> quadding (left/center/right). Uses a post-save dict patch
    /// because PDFium has GetNumberValue but no SetNumberValue.
    /// </summary>
    Task<PdfAnnotationInfo> SetTextQuaddingAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        PdfTextQuadding quadding,
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

    /// <summary>
    /// Toggle underline for a FreeText text box/callout. Persists <c>GlyphUnderline</c> and
    /// draws a companion ink underline stroke (hidden from the annotation sidebar).
    /// </summary>
    Task<PdfAnnotationInfo> SetUnderlineAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        bool underline,
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

    /// <summary>
    /// Reads stroke/border width in points when PDFium exposes it (ink, square, circle, FreeText).
    /// </summary>
    Task<float?> GetBorderWidthAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates stroke/border width in points for ink and shape annotations that support borders.
    /// </summary>
    Task SetBorderWidthAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        float borderWidthPoints,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Set fill/interior color for square, circle, and FreeText annotations.
    /// Pass <c>null</c> to clear the fill when supported (sets transparent alpha).
    /// </summary>
    Task SetFillColorAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        PdfAnnotationColor? fillColor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reposition the ink pointer tip for a FreeText callout (Subj=Callout), keeping the text box fixed.
    /// </summary>
    Task SetCalloutTipAsync(
        IPdfDocument document,
        int pageIndex,
        int calloutAnnotIndex,
        PdfPagePoint tip,
        float pointerWidthPoints = 1.5f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assign a shared group id to the given annotations (same page). Pass null to clear.
    /// Stored in annotation dictionary key <c>GlyphGroup</c>.
    /// </summary>
    Task SetGroupAsync(
        IPdfDocument document,
        IReadOnlyList<(int PageIndex, int AnnotIndex)> annots,
        string? groupId,
        CancellationToken cancellationToken = default);

    Task MoveAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        PdfRect bounds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotate an annotation by a multiple of 90° clockwise where supported
    /// (stamps, ink/shapes, FreeText, square/circle). Sticky notes and text markup are not rotated.
    /// </summary>
    Task<PdfAnnotationInfo> RotateAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        int degreesClockwise,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a line/arrow ink shape with new endpoints, preserving stroke style metadata.
    /// </summary>
    Task<PdfAnnotationInfo> SetLineEndpointsAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        PdfPagePoint start,
        PdfPagePoint end,
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
