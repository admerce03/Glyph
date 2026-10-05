namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Stroke width presets for shape/ink style picker (F17-16/17).
/// </summary>
public static class PdfStrokeWidthPresets
{
    public static IReadOnlyList<float> Points { get; } = [1f, 2f, 3f, 5f, 8f];

    public static string FormatLabel(float points) => $"{points:0} pt";

    public static int IndexOfNearest(float widthPoints, float tolerance = 0.01f)
    {
        for (var i = 0; i < Points.Count; i++)
        {
            if (Math.Abs(Points[i] - widthPoints) < tolerance)
            {
                return i;
            }
        }

        return -1;
    }

    public static int DefaultSelectedIndex(float currentWidth)
    {
        var idx = IndexOfNearest(currentWidth);
        return idx >= 0 ? idx : 1;
    }
}
