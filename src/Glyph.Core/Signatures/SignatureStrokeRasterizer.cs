namespace Glyph.Core.Signatures;

/// <summary>
/// Rasterize mouse signature strokes to a transparent BGRA bitmap.
/// Coordinates are in PDF page space (points, origin bottom-left).
/// </summary>
public static class SignatureStrokeRasterizer
{
    public static SignatureRaster Rasterize(
        IReadOnlyList<(double X, double Y)> points,
        double strokeWidthPoints = 2.75,
        double paddingPoints = 10,
        double pixelsPerPoint = 3)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2)
        {
            throw new ArgumentException("Need at least two points to rasterize a signature.", nameof(points));
        }

        if (strokeWidthPoints <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strokeWidthPoints));
        }

        if (paddingPoints < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paddingPoints));
        }

        if (pixelsPerPoint <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelsPerPoint));
        }

        var minX = points[0].X;
        var maxX = points[0].X;
        var minY = points[0].Y;
        var maxY = points[0].Y;
        for (var i = 1; i < points.Count; i++)
        {
            var (x, y) = points[i];
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        var contentWidth = Math.Max(1, maxX - minX);
        var contentHeight = Math.Max(1, maxY - minY);
        var pad = paddingPoints + (strokeWidthPoints * 0.5);
        var widthPts = contentWidth + (pad * 2);
        var heightPts = contentHeight + (pad * 2);
        var width = Math.Max(1, (int)Math.Ceiling(widthPts * pixelsPerPoint));
        var height = Math.Max(1, (int)Math.Ceiling(heightPts * pixelsPerPoint));
        var pixels = new byte[width * height * 4];
        var radius = Math.Max(1, (int)Math.Round(strokeWidthPoints * pixelsPerPoint * 0.5));

        for (var i = 1; i < points.Count; i++)
        {
            var x0 = (points[i - 1].X - minX + pad) * pixelsPerPoint;
            var y0 = (maxY - points[i - 1].Y + pad) * pixelsPerPoint; // flip to top-left image space
            var x1 = (points[i].X - minX + pad) * pixelsPerPoint;
            var y1 = (maxY - points[i].Y + pad) * pixelsPerPoint;
            StampSegment(pixels, width, height, x0, y0, x1, y1, radius);
        }

        return new SignatureRaster(pixels, width, height, contentWidth, contentHeight, pad);
    }

    private static void StampSegment(
        byte[] pixels,
        int width,
        int height,
        double x0,
        double y0,
        double x1,
        double y1,
        int radius)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        var dist = Math.Sqrt((dx * dx) + (dy * dy));
        var steps = Math.Max(1, (int)Math.Ceiling(dist));
        for (var s = 0; s <= steps; s++)
        {
            var t = s / (double)steps;
            var x = x0 + (dx * t);
            var y = y0 + (dy * t);
            StampDisk(pixels, width, height, x, y, radius);
        }
    }

    private static void StampDisk(byte[] pixels, int width, int height, double cx, double cy, int radius)
    {
        var r2 = radius * radius;
        var minX = Math.Max(0, (int)Math.Floor(cx - radius));
        var maxX = Math.Min(width - 1, (int)Math.Ceiling(cx + radius));
        var minY = Math.Max(0, (int)Math.Floor(cy - radius));
        var maxY = Math.Min(height - 1, (int)Math.Ceiling(cy + radius));
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var ddx = x + 0.5 - cx;
                var ddy = y + 0.5 - cy;
                if ((ddx * ddx) + (ddy * ddy) > r2)
                {
                    continue;
                }

                var i = ((y * width) + x) * 4;
                // Opaque black ink on transparent background (BGRA).
                pixels[i] = 20;
                pixels[i + 1] = 20;
                pixels[i + 2] = 20;
                pixels[i + 3] = 255;
            }
        }
    }
}
