namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Axis-aligned rectangle in PDF page space (points, origin bottom-left).
/// </summary>
public readonly record struct PdfRect(double Left, double Bottom, double Right, double Top)
{
    public double Width => Math.Max(0, Right - Left);

    public double Height => Math.Max(0, Top - Bottom);

    public bool Intersects(PdfRect other) =>
        Left < other.Right && Right > other.Left && Bottom < other.Top && Top > other.Bottom;

    public bool ContainsPoint(double x, double y) =>
        x >= Left && x <= Right && y >= Bottom && y <= Top;
}
