namespace Glyph.Core.Pdf;

/// <summary>
/// Detects mixed page sizes within a document (F04-07).
/// </summary>
public static class PdfPageSizeSet
{
    public readonly record struct Size(double WidthPoints, double HeightPoints);

    public static bool HasMixedSizes(IEnumerable<Size> sizes, double tolerancePoints = 0.5)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        Size? first = null;
        foreach (var size in sizes)
        {
            if (first is null)
            {
                first = size;
                continue;
            }

            if (Math.Abs(size.WidthPoints - first.Value.WidthPoints) > tolerancePoints
                || Math.Abs(size.HeightPoints - first.Value.HeightPoints) > tolerancePoints)
            {
                return true;
            }
        }

        return false;
    }
}
