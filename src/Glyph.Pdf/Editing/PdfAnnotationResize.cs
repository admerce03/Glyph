using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Editing;

/// <summary>
/// Compute new annotation bounds when dragging a selection resize handle.
/// Handle ids: n, s, e, w, and corners nw/ne/sw/se (PDF Y-up coordinates).
/// </summary>
public static class PdfAnnotationResize
{
    public const double DefaultMinSizePoints = 8.0;

    public static PdfRect ComputeBounds(
        PdfRect origin,
        string handle,
        double deltaXPoints,
        double deltaYPoints,
        double minSizePoints = DefaultMinSizePoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        if (minSizePoints <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minSizePoints));
        }

        var left = origin.Left;
        var bottom = origin.Bottom;
        var right = origin.Right;
        var top = origin.Top;

        if (handle.Contains('w', StringComparison.Ordinal))
        {
            left = Math.Min(origin.Right - minSizePoints, origin.Left + deltaXPoints);
        }

        if (handle.Contains('e', StringComparison.Ordinal))
        {
            right = Math.Max(origin.Left + minSizePoints, origin.Right + deltaXPoints);
        }

        if (handle.Contains('s', StringComparison.Ordinal))
        {
            bottom = Math.Min(origin.Top - minSizePoints, origin.Bottom + deltaYPoints);
        }

        if (handle.Contains('n', StringComparison.Ordinal))
        {
            top = Math.Max(origin.Bottom + minSizePoints, origin.Top + deltaYPoints);
        }

        return new PdfRect(left, bottom, right, top);
    }
}
