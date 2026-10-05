namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Engine-agnostic PDF annotation snapshot used by the sidebar and tests.
/// </summary>
public sealed record PdfAnnotation(
    string Id,
    int PageIndex,
    int AnnotIndex,
    PdfAnnotationKind Kind,
    PdfRect Bounds,
    PdfAnnotationColor Color,
    string? Contents,
    string? Author,
    IReadOnlyList<PdfQuad> Quads,
    string? SelectedText);

/// <summary>
/// Quadrilateral in PDF user space (points), matching PDFium QuadPoints ordering.
/// </summary>
public readonly record struct PdfQuad(
    double X1,
    double Y1,
    double X2,
    double Y2,
    double X3,
    double Y3,
    double X4,
    double Y4);

/// <summary>
/// Request to create a text-markup annotation (highlight / underline / strikeout).
/// </summary>
public sealed record PdfTextMarkupRequest(
    int PageIndex,
    PdfAnnotationKind Kind,
    IReadOnlyList<PdfQuad> Quads,
    PdfAnnotationColor Color,
    string? SelectedText = null,
    string? Author = null);

/// <summary>
/// Request to create a sticky-note (Text) annotation.
/// </summary>
public sealed record PdfStickyNoteRequest(
    int PageIndex,
    double X,
    double Y,
    string Contents,
    PdfAnnotationColor Color,
    string? Author = null);
