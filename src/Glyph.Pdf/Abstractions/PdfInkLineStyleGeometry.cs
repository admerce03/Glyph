namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Splits straight segments into ink strokes for dashed/dotted appearance.
/// </summary>
public static class PdfInkLineStyleGeometry
{
    public static IReadOnlyList<(PdfPagePoint Start, PdfPagePoint End)> Segment(
        PdfPagePoint start,
        PdfPagePoint end,
        PdfInkLineStyle style,
        float strokeWidthPoints)
    {
        if (style == PdfInkLineStyle.Solid)
        {
            return [(start, end)];
        }

        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 0.5)
        {
            return [(start, end)];
        }

        var ux = dx / length;
        var uy = dy / length;
        var dash = style == PdfInkLineStyle.Dotted
            ? Math.Max(1.5, strokeWidthPoints * 0.85)
            : Math.Max(4.0, strokeWidthPoints * 3.0);
        var gap = style == PdfInkLineStyle.Dotted
            ? Math.Max(3.0, strokeWidthPoints * 2.5)
            : Math.Max(3.0, strokeWidthPoints * 2.0);

        var segments = new List<(PdfPagePoint, PdfPagePoint)>();
        var traveled = 0.0;
        while (traveled < length)
        {
            var segStart = new PdfPagePoint(start.X + (ux * traveled), start.Y + (uy * traveled));
            var segLen = Math.Min(dash, length - traveled);
            var segEnd = new PdfPagePoint(
                segStart.X + (ux * segLen),
                segStart.Y + (uy * segLen));
            if (segLen >= 0.5)
            {
                segments.Add((segStart, segEnd));
            }

            traveled += dash + gap;
        }

        return segments.Count > 0 ? segments : [(start, end)];
    }

    public static IReadOnlyList<IReadOnlyList<PdfPagePoint>> ToInkStrokes(
        IReadOnlyList<(PdfPagePoint Start, PdfPagePoint End)> segments) =>
        segments.Select(s => (IReadOnlyList<PdfPagePoint>)[s.Start, s.End]).ToList();
}
