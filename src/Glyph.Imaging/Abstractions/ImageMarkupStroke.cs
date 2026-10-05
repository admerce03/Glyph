namespace Glyph.Imaging.Abstractions;

/// <summary>
/// A freehand markup stroke in document pixel space (origin top-left).
/// Kept as an overlay until flatten / save / export.
/// </summary>
public sealed class ImageMarkupStroke
{
    public ImageMarkupStroke(
        IReadOnlyList<ImageMarkupPoint> points,
        byte a,
        byte r,
        byte g,
        byte b,
        double widthPixels)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2)
        {
            throw new ArgumentException("Stroke needs at least two points.", nameof(points));
        }

        Points = points;
        A = a;
        R = r;
        G = g;
        B = b;
        WidthPixels = Math.Max(1, widthPixels);
    }

    public IReadOnlyList<ImageMarkupPoint> Points { get; }
    public byte A { get; }
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public double WidthPixels { get; }
}

public readonly record struct ImageMarkupPoint(double X, double Y);
