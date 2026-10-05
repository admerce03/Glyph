namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Quadrilateral in PDF page space (points, origin bottom-left).
/// Vertices are ordered BL → BR → TR → TL (counter-clockwise from lower-left).
/// </summary>
public readonly record struct PdfQuad(
    double X1,
    double Y1,
    double X2,
    double Y2,
    double X3,
    double Y3,
    double X4,
    double Y4)
{
    public static PdfQuad FromRect(PdfRect rect) =>
        new(rect.Left, rect.Bottom, rect.Right, rect.Bottom, rect.Right, rect.Top, rect.Left, rect.Top);

    public PdfRect Bounds
    {
        get
        {
            var left = Math.Min(Math.Min(X1, X2), Math.Min(X3, X4));
            var right = Math.Max(Math.Max(X1, X2), Math.Max(X3, X4));
            var bottom = Math.Min(Math.Min(Y1, Y2), Math.Min(Y3, Y4));
            var top = Math.Max(Math.Max(Y1, Y2), Math.Max(Y3, Y4));
            return new PdfRect(left, bottom, right, top);
        }
    }
}
