namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Geometry helpers for rotating annotation content around a center point.
/// Degrees are clockwise in PDF user space (Y-up).
/// </summary>
public static class PdfAnnotationRotate
{
    public static PdfPagePoint RotatePoint(PdfPagePoint point, PdfPagePoint center, int degreesClockwise)
    {
        var turns = NormalizeQuarterTurns(degreesClockwise);
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return turns switch
        {
            0 => point,
            1 => new PdfPagePoint(center.X + dy, center.Y - dx),
            2 => new PdfPagePoint(center.X - dx, center.Y - dy),
            3 => new PdfPagePoint(center.X - dy, center.Y + dx),
            _ => point,
        };
    }

    public static PdfRect RotateBounds(PdfRect bounds, int degreesClockwise)
    {
        var turns = NormalizeQuarterTurns(degreesClockwise);
        if (turns is 0 or 2)
        {
            if (turns == 0)
            {
                return bounds;
            }

            // 180°: same AABB.
            return bounds;
        }

        var cx = (bounds.Left + bounds.Right) / 2;
        var cy = (bounds.Bottom + bounds.Top) / 2;
        var halfW = bounds.Width / 2;
        var halfH = bounds.Height / 2;
        // 90/270: swap width/height around center.
        return new PdfRect(cx - halfH, cy - halfW, cx + halfH, cy + halfW);
    }

    public static PdfPagePoint BoundsCenter(PdfRect bounds) =>
        new((bounds.Left + bounds.Right) / 2, (bounds.Bottom + bounds.Top) / 2);

    /// <summary>
    /// Rotate a BGRA32 buffer by 90° clockwise (image Y-down coordinates).
    /// </summary>
    public static byte[] RotateBgra90Clockwise(
        ReadOnlySpan<byte> src,
        int width,
        int height,
        out int destWidth,
        out int destHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var expected = checked(width * height * 4);
        if (src.Length < expected)
        {
            throw new ArgumentException($"BGRA buffer shorter than {expected} bytes.", nameof(src));
        }

        destWidth = height;
        destHeight = width;
        var dest = new byte[destWidth * destHeight * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var sx = x;
                var sy = y;
                var dx = height - 1 - y;
                var dy = x;
                var si = ((sy * width) + sx) * 4;
                var di = ((dy * destWidth) + dx) * 4;
                dest[di] = src[si];
                dest[di + 1] = src[si + 1];
                dest[di + 2] = src[si + 2];
                dest[di + 3] = src[si + 3];
            }
        }

        return dest;
    }

    public static byte[] RotateBgra(
        ReadOnlySpan<byte> src,
        int width,
        int height,
        int degreesClockwise,
        out int destWidth,
        out int destHeight)
    {
        var turns = NormalizeQuarterTurns(degreesClockwise);
        destWidth = width;
        destHeight = height;
        var current = src.ToArray();
        var w = width;
        var h = height;
        for (var i = 0; i < turns; i++)
        {
            current = RotateBgra90Clockwise(current, w, h, out w, out h);
        }

        destWidth = w;
        destHeight = h;
        return current;
    }

    public static int NormalizeQuarterTurns(int degreesClockwise)
    {
        var d = degreesClockwise % 360;
        if (d < 0)
        {
            d += 360;
        }

        return d switch
        {
            0 => 0,
            90 => 1,
            180 => 2,
            270 => 3,
            _ => throw new ArgumentOutOfRangeException(
                nameof(degreesClockwise),
                degreesClockwise,
                "Rotation must be a multiple of 90 degrees."),
        };
    }
}
