namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Sobel edge map + snap helpers for smart lasso (F28-04 / F29-01/02).
/// </summary>
public static class ImageSmartLassoEdges
{
    public const int DefaultSnapRadius = 8;

    public static float[,] LuminanceFromBgra(ReadOnlySpan<byte> bgra, int width, int height)
    {
        var lum = new float[height, width];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = ((y * width) + x) * 4;
                lum[y, x] = (bgra[i] * 0.114f) + (bgra[i + 1] * 0.587f) + (bgra[i + 2] * 0.299f);
            }
        }

        return lum;
    }

    public static float[,] ComputeSobel(float[,] luminance)
    {
        var h = luminance.GetLength(0);
        var w = luminance.GetLength(1);
        var edges = new float[h, w];
        for (var y = 1; y < h - 1; y++)
        {
            for (var x = 1; x < w - 1; x++)
            {
                var gx = -luminance[y - 1, x - 1] - (2 * luminance[y, x - 1]) - luminance[y + 1, x - 1]
                    + luminance[y - 1, x + 1] + (2 * luminance[y, x + 1]) + luminance[y + 1, x + 1];
                var gy = -luminance[y - 1, x - 1] - (2 * luminance[y - 1, x]) - luminance[y - 1, x + 1]
                    + luminance[y + 1, x - 1] + (2 * luminance[y + 1, x]) + luminance[y + 1, x + 1];
                edges[y, x] = MathF.Sqrt((gx * gx) + (gy * gy));
            }
        }

        return edges;
    }

    public static (int X, int Y) FindStrongestEdge(
        float[,] edges,
        int centerX,
        int centerY,
        int radius = DefaultSnapRadius)
    {
        var h = edges.GetLength(0);
        var w = edges.GetLength(1);
        var best = 0f;
        var bestX = centerX;
        var bestY = centerY;
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var x = centerX + dx;
                var y = centerY + dy;
                if (x < 0 || y < 0 || x >= w || y >= h)
                {
                    continue;
                }

                var strength = edges[y, x];
                if (strength > best)
                {
                    best = strength;
                    bestX = x;
                    bestY = y;
                }
            }
        }

        return (bestX, bestY);
    }
}
