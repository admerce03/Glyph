using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Glyph.App.Views;

/// <summary>
/// Crops a page bitmap to a PDF-space region and nearest-neighbor scales it for the loupe overlay.
/// </summary>
internal static class PdfLoupeMagnifier
{
    public const double DefaultZoom = 3.0;
    public const double PopupSizeDip = 168;

    /// <summary>
    /// Maps PDF page-space bounds (bottom-left origin) onto a top-left bitmap and returns a scaled crop.
    /// </summary>
    public static WriteableBitmap? CropAndScale(
        WriteableBitmap source,
        double pageWidthPoints,
        double pageHeightPoints,
        double left,
        double bottom,
        double right,
        double top,
        double zoom,
        int outSize)
    {
        if (source.PixelWidth <= 0 || source.PixelHeight <= 0 || pageWidthPoints <= 0 || pageHeightPoints <= 0)
        {
            return null;
        }

        if (outSize < 16 || zoom < 1.0)
        {
            return null;
        }

        var sx = source.PixelWidth / pageWidthPoints;
        var sy = source.PixelHeight / pageHeightPoints;
        var srcLeft = (int)Math.Floor(Math.Min(left, right) * sx);
        var srcRight = (int)Math.Ceiling(Math.Max(left, right) * sx);
        var srcTop = (int)Math.Floor((pageHeightPoints - Math.Max(bottom, top)) * sy);
        var srcBottom = (int)Math.Ceiling((pageHeightPoints - Math.Min(bottom, top)) * sy);

        srcLeft = Math.Clamp(srcLeft, 0, source.PixelWidth - 1);
        srcRight = Math.Clamp(srcRight, srcLeft + 1, source.PixelWidth);
        srcTop = Math.Clamp(srcTop, 0, source.PixelHeight - 1);
        srcBottom = Math.Clamp(srcBottom, srcTop + 1, source.PixelHeight);

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
        sampleLeft = Math.Clamp(sampleLeft, 0, Math.Max(0, source.PixelWidth - sampleSize));
        sampleTop = Math.Clamp(sampleTop, 0, Math.Max(0, source.PixelHeight - sampleSize));
        if (sampleLeft + sampleSize > source.PixelWidth)
        {
            sampleSize = source.PixelWidth - sampleLeft;
        }

        if (sampleTop + sampleSize > source.PixelHeight)
        {
            sampleSize = Math.Min(sampleSize, source.PixelHeight - sampleTop);
        }

        if (sampleSize < 2)
        {
            return null;
        }

        var srcPixels = new byte[source.PixelWidth * source.PixelHeight * 4];
        using (var stream = source.PixelBuffer.AsStream())
        {
            _ = stream.Read(srcPixels, 0, srcPixels.Length);
        }

        var dst = new WriteableBitmap(outSize, outSize);
        var dstPixels = new byte[outSize * outSize * 4];
        for (var y = 0; y < outSize; y++)
        {
            var syIdx = sampleTop + y * sampleSize / outSize;
            if (syIdx >= source.PixelHeight)
            {
                syIdx = source.PixelHeight - 1;
            }

            for (var x = 0; x < outSize; x++)
            {
                var sxIdx = sampleLeft + x * sampleSize / outSize;
                if (sxIdx >= source.PixelWidth)
                {
                    sxIdx = source.PixelWidth - 1;
                }

                var si = (syIdx * source.PixelWidth + sxIdx) * 4;
                var di = (y * outSize + x) * 4;
                dstPixels[di] = srcPixels[si];
                dstPixels[di + 1] = srcPixels[si + 1];
                dstPixels[di + 2] = srcPixels[si + 2];
                dstPixels[di + 3] = srcPixels[si + 3];
            }
        }

        using (var stream = dst.PixelBuffer.AsStream())
        {
            stream.Write(dstPixels, 0, dstPixels.Length);
        }

        dst.Invalidate();
        return dst;
    }
}
