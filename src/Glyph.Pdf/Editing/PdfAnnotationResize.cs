using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Editing;

/// <summary>
/// Compute new annotation bounds when dragging a selection resize handle.
/// Handle ids: n, s, e, w, and corners nw/ne/sw/se (PDF Y-up coordinates).
/// Line/arrow endpoint handles use ids p0 and p1.
/// </summary>
public static class PdfAnnotationResize
{
    public const double DefaultMinSizePoints = 8.0;
    public const double DefaultMinEndpointDistancePoints = 2.0;

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

    /// <summary>
    /// Moves one endpoint of a line/arrow. Returns the new endpoints.
    /// </summary>
    public static (PdfPagePoint A, PdfPagePoint B) ComputeEndpoints(
        PdfPagePoint a,
        PdfPagePoint b,
        string handle,
        double deltaXPoints,
        double deltaYPoints,
        double minDistancePoints = DefaultMinEndpointDistancePoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        if (minDistancePoints < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minDistancePoints));
        }

        var nextA = a;
        var nextB = b;
        if (handle is "p0" or "start")
        {
            nextA = new PdfPagePoint(a.X + deltaXPoints, a.Y + deltaYPoints);
        }
        else if (handle is "p1" or "end")
        {
            nextB = new PdfPagePoint(b.X + deltaXPoints, b.Y + deltaYPoints);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(handle), handle, "Expected p0/p1 endpoint handle.");
        }

        var dx = nextB.X - nextA.X;
        var dy = nextB.Y - nextA.Y;
        var dist = Math.Sqrt((dx * dx) + (dy * dy));
        if (dist < minDistancePoints)
        {
            // Reject collapse: keep original endpoints.
            return (a, b);
        }

        return (nextA, nextB);
    }

    public static bool IsEndpointHandle(string? handle) =>
        handle is "p0" or "p1" or "start" or "end";
}
