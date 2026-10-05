namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Lightweight annotation descriptor for listing and navigation.
/// </summary>
public sealed record PdfAnnotationInfo(
    int PageIndex,
    int AnnotIndex,
    PdfTextMarkupKind? TextMarkupKind,
    PdfRect Bounds,
    PdfAnnotationColor? Color);
