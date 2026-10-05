namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Builds a closed 5-point star polyline inscribed in PDF page bounds.
/// </summary>
public static class PdfStarGeometry
{
    /// <summary>
    /// Returns 11 points (5 outer/inner vertices + closing repeat of the first) in page space.
    /// </summary>
    public static IReadOnlyList<PdfPagePoint> BuildPoints(PdfRect bounds)
    {
        var cx = (bounds.Left + bounds.Right) / 2;
        var cy = (bounds.Bottom + bounds.Top) / 2;
        var rx = Math.Max(bounds.Width / 2, 0.5);
        var ry = Math.Max(bounds.Height / 2, 0.5);
        var innerScale = 0.38;

        // Point-up star: outer tips at 90° + k*72°, inner at +36°.
        var points = new List<PdfPagePoint>(11);
        for (var i = 0; i < 10; i++)
        {
            var angle = (Math.PI / 2) + (i * Math.PI / 5);
            var scale = (i % 2 == 0) ? 1.0 : innerScale;
            points.Add(new PdfPagePoint(
                cx + (Math.Cos(angle) * rx * scale),
                cy + (Math.Sin(angle) * ry * scale)));
        }

        points.Add(points[0]);
        return points;
    }
}
