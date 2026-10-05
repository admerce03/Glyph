namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Axis-aligned or two-point shape markup in document pixel space.
/// Kept as an overlay until flatten / save / export.
/// </summary>
public sealed class ImageMarkupShape
{
    public ImageMarkupShape(
        ImageMarkupShapeKind kind,
        double x1,
        double y1,
        double x2,
        double y2,
        byte a,
        byte r,
        byte g,
        byte b,
        double widthPixels,
        string? text = null,
        double fontSizePixels = 16)
    {
        Kind = kind;
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
        A = a;
        R = r;
        G = g;
        B = b;
        WidthPixels = Math.Max(1, widthPixels);
        Text = text;
        FontSizePixels = Math.Max(6, fontSizePixels);
    }

    public ImageMarkupShapeKind Kind { get; }
    public double X1 { get; }
    public double Y1 { get; }
    public double X2 { get; }
    public double Y2 { get; }
    public byte A { get; }
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public double WidthPixels { get; }
    public string? Text { get; }
    public double FontSizePixels { get; }
}

public enum ImageMarkupShapeKind
{
    Rectangle = 0,
    Ellipse = 1,
    Line = 2,
    Arrow = 3,
    Text = 4,
}

/// <summary>
/// Pending non-destructive image markup to bake on flatten/save.
/// </summary>
public sealed class ImageMarkupLayer
{
    public ImageMarkupLayer(
        IReadOnlyList<ImageMarkupStroke>? strokes = null,
        IReadOnlyList<ImageMarkupShape>? shapes = null)
    {
        Strokes = strokes ?? Array.Empty<ImageMarkupStroke>();
        Shapes = shapes ?? Array.Empty<ImageMarkupShape>();
    }

    public IReadOnlyList<ImageMarkupStroke> Strokes { get; }
    public IReadOnlyList<ImageMarkupShape> Shapes { get; }

    public int Count => Strokes.Count + Shapes.Count;

    public bool IsEmpty => Count == 0;
}
