namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Vector shape annotation kinds backed by standard PDF subtypes.
/// </summary>
public enum PdfShapeKind
{
    Rectangle = 0,
    Ellipse = 1,
    Line = 2,
    Arrow = 3,
    Freeform = 4,
    RoundedRectangle = 5,
    HighlightRectangle = 6,
    Star = 7,
    Polygon = 8,
}
