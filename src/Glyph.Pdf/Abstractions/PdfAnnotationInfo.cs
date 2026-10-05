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
    string? GroupId = null);
