namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Maps display/DIP coordinates (origin top-left, Y down) to PDF page space
/// (origin bottom-left, Y up) and always returns normalized rects.
/// </summary>
public static class PdfPageCoordinates
{
    public static PdfPagePoint FromDisplayPoint(
        double displayX,
        double displayY,
        double pageHeightPoints,
        double scale)
    {
        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        return new PdfPagePoint(displayX / scale, pageHeightPoints - (displayY / scale));
    }

    /// <summary>
    /// Maps a display-space axis-aligned rect (any corner order) to a normalized
    /// PDF <see cref="PdfRect"/> with Bottom ≤ Top and Left ≤ Right.
    /// </summary>
    public static PdfRect FromDisplayRect(
        double displayLeft,
        double displayTop,
        double displayRight,
        double displayBottom,
        double pageHeightPoints,
        double scale)
    {
        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        var left = Math.Min(displayLeft, displayRight) / scale;
        var right = Math.Max(displayLeft, displayRight) / scale;
        var uiTop = Math.Min(displayTop, displayBottom);
        var uiBottom = Math.Max(displayTop, displayBottom);
        // UI top (small Y) → high PDF Y; UI bottom (large Y) → low PDF Y.
        var pdfTop = pageHeightPoints - (uiTop / scale);
        var pdfBottom = pageHeightPoints - (uiBottom / scale);
        return new PdfRect(
            left,
            Math.Min(pdfBottom, pdfTop),
            right,
            Math.Max(pdfBottom, pdfTop));
    }

    public static PdfRect FromDisplayRectXywh(
        double displayX,
        double displayY,
        double displayWidth,
        double displayHeight,
        double pageHeightPoints,
        double scale) =>
        FromDisplayRect(
            displayX,
            displayY,
            displayX + displayWidth,
            displayY + displayHeight,
            pageHeightPoints,
            scale);
}
