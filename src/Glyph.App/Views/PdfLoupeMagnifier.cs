using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.Core.Pdf;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Glyph.App.Views;

/// <summary>
/// Crops a page bitmap to a PDF-space region and nearest-neighbor scales it for the loupe overlay.
/// </summary>
internal static class PdfLoupeMagnifier
{
    public const double DefaultZoom = PdfLoupeSampleRegion.DefaultZoom;
    public const double PopupSizeDip = PdfLoupeSampleRegion.PopupSizeDip;

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
        if (outSize < 16)
        {
            return null;
        }

        var sample = PdfLoupeSampleRegion.Resolve(
            source.PixelWidth,
            source.PixelHeight,
            pageWidthPoints,
            pageHeightPoints,
            left,
            bottom,
            right,
            top,
            zoom);
        if (sample is null)
        {
            return null;
        }

        var sampleLeft = sample.Value.Left;
        var sampleTop = sample.Value.Top;
        var sampleSize = sample.Value.Size;

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
