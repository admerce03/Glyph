namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Maps PDF user-space redaction bounds to top-left canvas pixels for preview overlays (F21-03).
/// PDF y grows upward; WinUI Canvas y grows downward.
/// </summary>
public static class PdfRedactionOverlayLayout
{
    public readonly record struct CanvasRect(double Left, double Top, double Width, double Height);

    /// <summary>
    /// Returns false when the mapped rect would be smaller than 1×1 px (not worth drawing).
    /// </summary>
    public static bool TryMapToCanvas(
        PdfRect bounds,
        double pageHeightPoints,
        double scale,
        out CanvasRect canvas)
    {
        var width = bounds.Width * scale;
        var height = bounds.Height * scale;
        if (width < 1 || height < 1 || scale <= 0 || pageHeightPoints <= 0)
        {
            canvas = default;
            return false;
        }

        canvas = new CanvasRect(
            Left: bounds.Left * scale,
            Top: (pageHeightPoints - bounds.Top) * scale,
            Width: width,
            Height: height);
        return true;
    }
}
