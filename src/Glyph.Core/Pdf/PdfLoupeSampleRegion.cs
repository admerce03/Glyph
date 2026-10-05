namespace Glyph.Core.Pdf;

/// <summary>
/// Pure geometry for the PDF magnifier/loupe sample region (F04-30).
/// PDF page-space uses bottom-left origin; bitmaps use top-left.
/// </summary>
public static class PdfLoupeSampleRegion
{
    public const double DefaultZoom = 3.0;
    public const double PopupSizeDip = 168;

    public readonly record struct PixelRect(int Left, int Top, int Size);

    /// <summary>
    /// Maps a PDF-space rect onto bitmap pixels and returns the square sample to enlarge.
    /// Returns null when inputs are invalid or the sample would be empty.
    /// </summary>
    public static PixelRect? Resolve(
        int bitmapWidth,
        int bitmapHeight,
        double pageWidthPoints,
        double pageHeightPoints,
        double left,
        double bottom,
        double right,
        double top,
        double zoom)
    {
        if (bitmapWidth <= 0 || bitmapHeight <= 0 || pageWidthPoints <= 0 || pageHeightPoints <= 0)
        {
            return null;
        }

        if (zoom < 1.0)
        {
            return null;
        }

        var sx = bitmapWidth / pageWidthPoints;
        var sy = bitmapHeight / pageHeightPoints;
        var srcLeft = (int)Math.Floor(Math.Min(left, right) * sx);
        var srcRight = (int)Math.Ceiling(Math.Max(left, right) * sx);
        var srcTop = (int)Math.Floor((pageHeightPoints - Math.Max(bottom, top)) * sy);
        var srcBottom = (int)Math.Ceiling((pageHeightPoints - Math.Min(bottom, top)) * sy);

        srcLeft = Math.Clamp(srcLeft, 0, bitmapWidth - 1);
        srcRight = Math.Clamp(srcRight, srcLeft + 1, bitmapWidth);
        srcTop = Math.Clamp(srcTop, 0, bitmapHeight - 1);
        srcBottom = Math.Clamp(srcBottom, srcTop + 1, bitmapHeight);

        var regionW = srcRight - srcLeft;
        var regionH = srcBottom - srcTop;
        if (regionW < 1 || regionH < 1)
        {
            return null;
        }

        // At DefaultZoom, crop roughly the loupe interior; higher zoom → smaller sample.
        var sampleHalf = Math.Max(regionW, regionH) * 0.5 / Math.Max(1.0, zoom / DefaultZoom);
        var cx = (srcLeft + srcRight) * 0.5;
        var cy = (srcTop + srcBottom) * 0.5;
        var sampleLeft = (int)Math.Floor(cx - sampleHalf);
        var sampleTop = (int)Math.Floor(cy - sampleHalf);
        var sampleSize = Math.Max(2, (int)Math.Ceiling(sampleHalf * 2));
        sampleLeft = Math.Clamp(sampleLeft, 0, Math.Max(0, bitmapWidth - sampleSize));
        sampleTop = Math.Clamp(sampleTop, 0, Math.Max(0, bitmapHeight - sampleSize));
        if (sampleLeft + sampleSize > bitmapWidth)
        {
            sampleSize = bitmapWidth - sampleLeft;
        }

        if (sampleTop + sampleSize > bitmapHeight)
        {
            sampleSize = Math.Min(sampleSize, bitmapHeight - sampleTop);
        }

        if (sampleSize < 2)
        {
            return null;
        }

        return new PixelRect(sampleLeft, sampleTop, sampleSize);
    }
}
