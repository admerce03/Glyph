namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Recognized cleaned shape from a freehand stroke.
/// </summary>
public enum PdfRecognizedStrokeShape
{
    None = 0,
    Line = 1,
    Rectangle = 2,
    Ellipse = 3,
    Triangle = 4,
}

/// <summary>
/// Heuristic recognition of rough geometric shapes from ink polylines.
/// </summary>
public static class PdfStrokeShapeRecognizer
{
    public sealed record Result(
        PdfRecognizedStrokeShape Shape,
        PdfRect Bounds,
        IReadOnlyList<PdfPagePoint>? Vertices = null);

    public static Result Recognize(IReadOnlyList<PdfPagePoint> points)
    {
        if (points.Count < 2)
        {
            return new Result(PdfRecognizedStrokeShape.None, default);
        }

        var bounds = BoundsOf(points);
        if (bounds.Width < 4 && bounds.Height < 4)
        {
            return new Result(PdfRecognizedStrokeShape.None, bounds);
        }

        var size = Math.Max(bounds.Width, bounds.Height);
        var closed = IsClosed(points, Math.Max(8.0, size * 0.12));
        var simplified = Simplify(points, tolerance: Math.Max(2.0, size * 0.04));

        if (!closed)
        {
            if (IsLineLike(simplified.Count >= 2 ? simplified : points, bounds))
            {
                var a = points[0];
                var b = points[^1];
                return new Result(PdfRecognizedStrokeShape.Line, BoundsOf([a, b]), [a, b]);
            }

            return new Result(PdfRecognizedStrokeShape.None, bounds);
        }

        // Drop duplicate closing point for vertex counting.
        var verts = simplified.ToList();
        if (verts.Count >= 2 && Distance(verts[0], verts[^1]) <= Math.Max(8.0, size * 0.12))
        {
            verts.RemoveAt(verts.Count - 1);
        }

        // Merge near-duplicate consecutive vertices.
        verts = MergeNear(verts, Math.Max(4.0, size * 0.05));

        if (verts.Count == 3)
        {
            return new Result(PdfRecognizedStrokeShape.Triangle, BoundsOf(verts), verts);
        }

        if (verts.Count == 4 && LooksRectangular(verts, bounds))
        {
            return new Result(PdfRecognizedStrokeShape.Rectangle, bounds, verts);
        }

        if (IsEllipseLike(points, bounds))
        {
            return new Result(PdfRecognizedStrokeShape.Ellipse, bounds);
        }

        // Soft rectangle: closed + non-circular + 3–5 simplified verts.
        if (verts.Count is >= 3 and <= 5 && Circularity(points, bounds) < 0.72)
        {
            return new Result(PdfRecognizedStrokeShape.Rectangle, bounds);
        }

        return new Result(PdfRecognizedStrokeShape.None, bounds);
    }

    private static bool LooksRectangular(IReadOnlyList<PdfPagePoint> verts, PdfRect bounds)
    {
        if (verts.Count != 4)
        {
            return false;
        }

        var rightAngles = 0;
        for (var i = 0; i < 4; i++)
        {
            var ang = CornerAngleDegrees(verts[(i + 3) % 4], verts[i], verts[(i + 1) % 4]);
            if (ang is >= 55 and <= 125)
            {
                rightAngles++;
            }
        }

        if (rightAngles < 2)
        {
            return false;
        }

        var hull = BoundsOf(verts);
        var ratio = (hull.Width * hull.Height) / Math.Max(1.0, bounds.Width * bounds.Height);
        return ratio is >= 0.65 and <= 1.35;
    }

    private static bool IsClosed(IReadOnlyList<PdfPagePoint> points, double threshold)
    {
        var a = points[0];
        var b = points[^1];
        return Distance(a, b) <= threshold;
    }

    private static bool IsLineLike(IReadOnlyList<PdfPagePoint> points, PdfRect bounds)
    {
        if (points.Count < 2)
        {
            return false;
        }

        var length = Math.Sqrt((bounds.Width * bounds.Width) + (bounds.Height * bounds.Height));
        if (length < 12)
        {
            return false;
        }

        var a = points[0];
        var b = points[^1];
        var maxDev = 0.0;
        for (var i = 1; i < points.Count - 1; i++)
        {
            maxDev = Math.Max(maxDev, PerpDistance(a, b, points[i]));
        }

        return maxDev <= Math.Max(3.0, length * 0.06);
    }

    private static bool IsEllipseLike(IReadOnlyList<PdfPagePoint> points, PdfRect bounds)
    {
        if (points.Count < 8 || bounds.Width < 10 || bounds.Height < 10)
        {
            return false;
        }

        return Circularity(points, bounds) >= 0.78;
    }

    private static double Circularity(IReadOnlyList<PdfPagePoint> points, PdfRect bounds)
    {
        var cx = (bounds.Left + bounds.Right) / 2;
        var cy = (bounds.Bottom + bounds.Top) / 2;
        var rx = Math.Max(bounds.Width / 2, 1);
        var ry = Math.Max(bounds.Height / 2, 1);
        var err = 0.0;
        var n = 0;
        foreach (var p in points)
        {
            var nx = (p.X - cx) / rx;
            var ny = (p.Y - cy) / ry;
            var r = Math.Sqrt((nx * nx) + (ny * ny));
            err += Math.Abs(r - 1.0);
            n++;
        }

        return Math.Clamp(1.0 - (err / Math.Max(1, n)), 0, 1);
    }

    private static List<PdfPagePoint> MergeNear(List<PdfPagePoint> points, double minDist)
    {
        if (points.Count == 0)
        {
            return points;
        }

        var result = new List<PdfPagePoint> { points[0] };
        for (var i = 1; i < points.Count; i++)
        {
            if (Distance(result[^1], points[i]) >= minDist)
            {
                result.Add(points[i]);
            }
        }

        if (result.Count > 1 && Distance(result[0], result[^1]) < minDist)
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    private static List<PdfPagePoint> Simplify(IReadOnlyList<PdfPagePoint> points, double tolerance)
    {
        if (points.Count <= 2)
        {
            return points.ToList();
        }

        var keep = new bool[points.Count];
        keep[0] = true;
        keep[^1] = true;
        SimplifyRange(points, 0, points.Count - 1, tolerance, keep);
        var result = new List<PdfPagePoint>();
        for (var i = 0; i < points.Count; i++)
        {
            if (keep[i])
            {
                result.Add(points[i]);
            }
        }

        return result;
    }

    private static void SimplifyRange(
        IReadOnlyList<PdfPagePoint> points,
        int first,
        int last,
        double tolerance,
        bool[] keep)
    {
        if (last <= first + 1)
        {
            return;
        }

        var maxDist = 0.0;
        var index = first;
        for (var i = first + 1; i < last; i++)
        {
            var d = PerpDistance(points[first], points[last], points[i]);
            if (d > maxDist)
            {
                maxDist = d;
                index = i;
            }
        }

        if (maxDist > tolerance)
        {
            keep[index] = true;
            SimplifyRange(points, first, index, tolerance, keep);
            SimplifyRange(points, index, last, tolerance, keep);
        }
    }

    private static PdfRect BoundsOf(IReadOnlyList<PdfPagePoint> points)
    {
        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);
        return new PdfRect(minX, minY, maxX, maxY);
    }

    private static double PerpDistance(PdfPagePoint a, PdfPagePoint b, PdfPagePoint p)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len2 = (dx * dx) + (dy * dy);
        if (len2 < 1e-9)
        {
            return Distance(a, p);
        }

        var t = (((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / len2;
        var projX = a.X + (t * dx);
        var projY = a.Y + (t * dy);
        return Math.Sqrt(((p.X - projX) * (p.X - projX)) + ((p.Y - projY) * (p.Y - projY)));
    }

    private static double Distance(PdfPagePoint a, PdfPagePoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static double CornerAngleDegrees(PdfPagePoint a, PdfPagePoint b, PdfPagePoint c)
    {
        var v1x = a.X - b.X;
        var v1y = a.Y - b.Y;
        var v2x = c.X - b.X;
        var v2y = c.Y - b.Y;
        var n1 = Math.Sqrt((v1x * v1x) + (v1y * v1y));
        var n2 = Math.Sqrt((v2x * v2x) + (v2y * v2y));
        if (n1 < 1e-6 || n2 < 1e-6)
        {
            return 180;
        }

        var cos = Math.Clamp(((v1x * v2x) + (v1y * v2y)) / (n1 * n2), -1, 1);
        return Math.Acos(cos) * (180.0 / Math.PI);
    }
}
