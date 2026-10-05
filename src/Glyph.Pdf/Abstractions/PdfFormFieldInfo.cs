namespace Glyph.Pdf.Abstractions;

public sealed record PdfFormFieldInfo(
    int PageIndex,
    int AnnotIndex,
    string Name,
    PdfFormFieldKind Kind,
    string Value,
    PdfRect Bounds,
    int TabOrder);
