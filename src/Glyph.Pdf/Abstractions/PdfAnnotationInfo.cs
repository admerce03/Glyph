namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Lightweight annotation descriptor for listing and navigation.
/// </summary>
public sealed record PdfAnnotationInfo(
    int PageIndex,
    int AnnotIndex,
    PdfTextMarkupKind? TextMarkupKind,
    PdfRect Bounds,
    PdfAnnotationColor? Color,
    string? Contents = null,
    bool IsStickyNote = false,
    bool IsInk = false,
    PdfShapeKind? ShapeKind = null,
    bool IsTextBox = false,
    bool IsStamp = false,
    bool IsCallout = false,
    string? Author = null,
    string? GroupId = null,
    bool IsUnderlined = false,
    PdfPagePoint? EndpointA = null,
    PdfPagePoint? EndpointB = null,
    PdfTextQuadding? TextQuadding = null)
{
    /// <summary>
    /// True when this annotation exposes line/arrow endpoint handles instead of box handles.
    /// </summary>
    public bool UsesEndpointHandles =>
        ShapeKind is PdfShapeKind.Line or PdfShapeKind.Arrow
        && EndpointA is not null
        && EndpointB is not null;
}
