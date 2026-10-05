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

    /// <summary>
    /// Fraction of this rectangle covered by <paramref name="other"/> (0–1).
    /// Used by redaction sanitize to require meaningful overlap (F21-06).
    /// </summary>
    public double CoverageBy(PdfRect other)
    {
        var left = Math.Max(Left, other.Left);
        var bottom = Math.Max(Bottom, other.Bottom);
        var right = Math.Min(Right, other.Right);
        var top = Math.Min(Top, other.Top);
        if (right <= left || top <= bottom)
        {
            return 0;
        }

        var area = Width * Height;
        if (area <= 0)
        {
            return 0;
        }

        return ((right - left) * (top - bottom)) / area;
    }
}
