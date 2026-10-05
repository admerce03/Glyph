namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Builds a closed speech-bubble outline (rounded body + bottom-left pointer) in page space.
/// </summary>
public static class PdfSpeechBubbleGeometry
{
    /// <summary>
    /// Returns a closed polyline for a speech bubble inscribed in <paramref name="bounds"/>.
    /// The lower ~22% of the height is reserved for the triangular pointer.
    /// </summary>
    public static IReadOnlyList<PdfPagePoint> BuildPoints(PdfRect bounds)
    {
        var w = Math.Max(bounds.Width, 1.0);
        var h = Math.Max(bounds.Height, 1.0);
        var left = bounds.Left;
        var bottom = bounds.Bottom;
        var right = bounds.Right;
        var top = bounds.Top;

        var bodyBottom = bottom + (h * 0.22);
        var radius = Math.Clamp(Math.Min(w, top - bodyBottom) * 0.18, 4.0, 28.0);
        var tipX = left + (w * 0.22);
        var tipBaseLeft = left + (w * 0.12);
        var tipBaseRight = left + (w * 0.32);

        // Approximate rounded rectangle with a few corner points + pointer notch on bottom edge.
        var points = new List<PdfPagePoint>
        {
            // Start at tip, then walk clockwise around body.
            new(tipX, bottom),
            new(tipBaseRight, bodyBottom),
            new(right - radius, bodyBottom),
            new(right, bodyBottom + radius),
            new(right, top - radius),
            new(right - radius, top),
            new(left + radius, top),
            new(left, top - radius),
            new(left, bodyBottom + radius),
            new(left + radius, bodyBottom),
            new(tipBaseLeft, bodyBottom),
            new(tipX, bottom), // close
        };

        return points;
    }
}
