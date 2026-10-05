namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Supported PDF annotation subtypes for Glyph markup.
/// Values match PDFium <c>FPDF_ANNOT_*</c> constants where applicable.
/// </summary>
public enum PdfAnnotationKind
{
    Unknown = 0,
    Text = 1,
    FreeText = 3,
    Line = 4,
    Square = 5,
    Circle = 6,
    Polygon = 7,
    PolyLine = 8,
    Highlight = 9,
    Underline = 10,
    Squiggly = 11,
    StrikeOut = 12,
    Stamp = 13,
    Ink = 15,
}
