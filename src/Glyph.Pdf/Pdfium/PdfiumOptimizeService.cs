using System.Runtime.InteropServices;
using Glyph.Pdf.Abstractions;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumOptimizeService : IPdfOptimizeService
{
    private const int PageObjImage = 3;
    private readonly IPdfImageJpegEncoder? _jpegEncoder;

    public PdfiumOptimizeService(IPdfImageJpegEncoder? jpegEncoder = null)
    {
        _jpegEncoder = jpegEncoder;
    }

    public PdfOptimizeEstimate Estimate(IPdfDocument document, PdfOptimizeOptions? options = null)
    {
        var pdfium = RequirePdfium(document);
        var opts = Resolve(options);
        PdfiumLibrary.EnsureInitialized();
        lock (PdfiumSync.Gate)
        {
            pdfium.ThrowIfDisposed();
            var current = PdfiumDocumentSaver.SaveToBytes(pdfium.Handle).LongLength;
            var (eligible, _) = ScanImages(pdfium, opts, mutate: false);
            var attachments = Math.Max(0, fpdf_attachment.FPDFDocGetAttachmentCount(pdfium.Handle));
            // Rough estimate: each downsampled image shrinks ~proportionally to pixel area;
            // attachment unlink rarely shrinks bytes until a later rewrite, so leave as-is.
            var estimated = current;
            foreach (var ratio in eligible)
            {
                estimated -= (long)(current * 0.02 * (1.0 - ratio)); // soft heuristic only
            }

            estimated = Math.Max(estimated, current / 4);
            return new PdfOptimizeEstimate(current, estimated, eligible.Count, attachments);
        }
    }

    public Task<PdfOptimizeResult> OptimizeAsync(
        IPdfDocument document,
        PdfOptimizeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        var opts = Resolve(options);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var before = PdfiumDocumentSaver.SaveToBytes(pdfium.Handle).LongLength;
                    var (_, downsampled) = ScanImages(pdfium, opts, mutate: true);
                    var attachmentsRemoved = 0;
                    if (opts.RemoveEmbeddedAttachments)
                    {
                        for (var i = fpdf_attachment.FPDFDocGetAttachmentCount(pdfium.Handle) - 1; i >= 0; i--)
                        {
                            if (fpdf_attachment.FPDFDocDeleteAttachment(pdfium.Handle, i) != 0)
                            {
                                attachmentsRemoved++;
                            }
                        }
                    }

                    // RemoveMetadata is deferred — PDFium exposes GetMetaText only (see ADR-015 / F25 edit).
                    var after = PdfiumDocumentSaver.SaveToBytes(pdfium.Handle).LongLength;
                    if (downsampled > 0)
                    {
                        pdfium.NotifyAnnotationsChanged();
                    }

                    return new PdfOptimizeResult(downsampled, attachmentsRemoved, before, after);
                }
            },
            cancellationToken);
    }

    private static PdfOptimizeOptions Resolve(PdfOptimizeOptions? options)
    {
        if (options is null)
        {
            return PdfOptimizeOptions.FromPreset(PdfOptimizePreset.Balanced);
        }

        return options.Preset == PdfOptimizePreset.Custom
            ? options
            : PdfOptimizeOptions.FromPreset(options.Preset);
    }

    /// <summary>
    /// Returns per-image area scale factors (new/old) for estimate, plus mutate count.
    /// </summary>
    private (List<double> ScaleFactors, int Downsampled) ScanImages(
        PdfiumDocument pdfium,
        PdfOptimizeOptions opts,
        bool mutate)
    {
        var factors = new List<double>();
        var downsampled = 0;
        if (!opts.DownsampleImages)
        {
            return (factors, 0);
        }

        for (var pageIndex = 0; pageIndex < pdfium.PageCount; pageIndex++)
        {
            var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
            if (page is null)
            {
                continue;
            }

            try
            {
                var count = fpdf_edit.FPDFPageCountObjects(page);
                var dirty = false;
                for (var i = 0; i < count; i++)
                {
                    var obj = fpdf_edit.FPDFPageGetObject(page, i);
                    if (obj is null || fpdf_edit.FPDFPageObjGetType(obj) != PageObjImage)
                    {
                        continue;
                    }

                    if (!TryGetEffectiveDpi(page, obj, out var dpiX, out var dpiY, out var pixelW, out var pixelH))
                    {
                        continue;
                    }

                    var dpi = Math.Max(dpiX, dpiY);
                    if (dpi <= opts.DownsampleAboveDpi || pixelW < 2 || pixelH < 2)
                    {
                        continue;
                    }

                    if (opts.PreserveMonochrome)
                    {
                        using var meta = new FPDF_IMAGEOBJ_METADATA();
                        if (fpdf_edit.FPDFImageObjGetImageMetadata(obj, page, meta) != 0
                            && meta.BitsPerPixel is > 0 and <= 1)
                        {
                            continue;
                        }
                    }

                    var scale = opts.TargetDpi / dpi;
                    if (scale >= 0.98)
                    {
                        continue;
                    }

                    var newW = Math.Max(1, (int)Math.Round(pixelW * scale));
                    var newH = Math.Max(1, (int)Math.Round(pixelH * scale));
                    if (newW >= pixelW && newH >= pixelH)
                    {
                        continue;
                    }

                    factors.Add((double)(newW * newH) / (pixelW * pixelH));
                    if (!mutate)
                    {
                        continue;
                    }

                    if (DownsampleImageObject(page, obj, newW, newH, opts.JpegQuality))
                    {
                        downsampled++;
                        dirty = true;
                    }
                }

                if (dirty && fpdf_edit.FPDFPageGenerateContent(page) == 0)
                {
                    throw new InvalidOperationException(
                        $"Failed to generate page content after optimize on page {pageIndex + 1}.");
                }
            }
            finally
            {
                fpdfview.FPDF_ClosePage(page);
            }
        }

        return (factors, downsampled);
    }

    private static bool TryGetEffectiveDpi(
        FpdfPageT page,
        FpdfPageobjectT obj,
        out double dpiX,
        out double dpiY,
        out int pixelW,
        out int pixelH)
    {
        dpiX = dpiY = 0;
        pixelW = pixelH = 0;
        using var meta = new FPDF_IMAGEOBJ_METADATA();
        if (fpdf_edit.FPDFImageObjGetImageMetadata(obj, page, meta) != 0
            && meta.Width > 0
            && meta.Height > 0)
        {
            pixelW = (int)meta.Width;
            pixelH = (int)meta.Height;
            if (meta.HorizontalDpi > 1 && meta.VerticalDpi > 1)
            {
                dpiX = meta.HorizontalDpi;
                dpiY = meta.VerticalDpi;
                return true;
            }
        }

        float left = 0, bottom = 0, right = 0, top = 0;
        if (fpdf_edit.FPDFPageObjGetBounds(obj, ref left, ref bottom, ref right, ref top) == 0)
        {
            return false;
        }

        var displayW = Math.Abs(right - left);
        var displayH = Math.Abs(top - bottom);
        if (pixelW <= 0 || pixelH <= 0)
        {
            var bmp = fpdf_edit.FPDFImageObjGetBitmap(obj);
            if (bmp is null)
            {
                return false;
            }

            try
            {
                pixelW = fpdfview.FPDFBitmapGetWidth(bmp);
                pixelH = fpdfview.FPDFBitmapGetHeight(bmp);
            }
            finally
            {
                fpdfview.FPDFBitmapDestroy(bmp);
            }
        }

        if (pixelW <= 0 || pixelH <= 0 || displayW < 0.5 || displayH < 0.5)
        {
            return false;
        }

        // points → inches = /72; DPI = pixels / inches
        dpiX = pixelW * 72.0 / displayW;
        dpiY = pixelH * 72.0 / displayH;
        return true;
    }

    private bool DownsampleImageObject(FpdfPageT page, FpdfPageobjectT obj, int newW, int newH, int jpegQuality)
    {
        var srcBmp = fpdf_edit.FPDFImageObjGetBitmap(obj);
        if (srcBmp is null)
        {
            return false;
        }

        byte[] srcPixels;
        int srcW, srcH, srcStride, format;
        try
        {
            srcW = fpdfview.FPDFBitmapGetWidth(srcBmp);
            srcH = fpdfview.FPDFBitmapGetHeight(srcBmp);
            srcStride = fpdfview.FPDFBitmapGetStride(srcBmp);
            format = fpdfview.FPDFBitmapGetFormat(srcBmp);
            var buffer = fpdfview.FPDFBitmapGetBuffer(srcBmp);
            if (buffer == IntPtr.Zero || srcW <= 0 || srcH <= 0)
            {
                return false;
            }

            srcPixels = new byte[srcStride * srcH];
            Marshal.Copy(buffer, srcPixels, 0, srcPixels.Length);
        }
        finally
        {
            fpdfview.FPDFBitmapDestroy(srcBmp);
        }

        var srcBgra = ToBgra(srcPixels, srcW, srcH, srcStride, format);
        var dstBgra = ResizeBgraNearest(srcBgra, srcW, srcH, newW, newH);

        // JPEG quality rewrite is staged behind IPdfImageJpegEncoder, but PDFiumCore's
        // FPDF_FILEACCESS marshaling currently faults on LoadJpegFileInline, so we always
        // use SetBitmap here (pixels correct; byte size may not shrink).
        _ = _jpegEncoder;
        _ = jpegQuality;

        var handle = GCHandle.Alloc(dstBgra, GCHandleType.Pinned);
        FpdfBitmapT? dstBmp = null;
        try
        {
            dstBmp = fpdfview.FPDFBitmapCreateEx(
                newW,
                newH,
                PdfiumBitmapFormats.Bgra,
                handle.AddrOfPinnedObject(),
                newW * 4);
            if (dstBmp is null)
            {
                return false;
            }

            return fpdf_edit.FPDFImageObjSetBitmap(page, 1, obj, dstBmp) != 0;
        }
        finally
        {
            if (dstBmp is not null)
            {
                fpdfview.FPDFBitmapDestroy(dstBmp);
            }

            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }
    }

    private static byte[] ToBgra(byte[] src, int width, int height, int stride, int format)
    {
        var dst = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var srcRow = y * stride;
            var dstRow = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                var di = dstRow + x * 4;
                switch (format)
                {
                    case PdfiumBitmapFormats.Bgra:
                    case PdfiumBitmapFormats.Bgrx:
                        {
                            var si = srcRow + x * 4;
                            dst[di] = src[si];
                            dst[di + 1] = src[si + 1];
                            dst[di + 2] = src[si + 2];
                            dst[di + 3] = format == PdfiumBitmapFormats.Bgra ? src[si + 3] : (byte)255;
                            break;
                        }

                    case PdfiumBitmapFormats.Bgr:
                        {
                            var si = srcRow + x * 3;
                            dst[di] = src[si];
                            dst[di + 1] = src[si + 1];
                            dst[di + 2] = src[si + 2];
                            dst[di + 3] = 255;
                            break;
                        }

                    case PdfiumBitmapFormats.Gray:
                        {
                            var g = src[srcRow + x];
                            dst[di] = g;
                            dst[di + 1] = g;
                            dst[di + 2] = g;
                            dst[di + 3] = 255;
                            break;
                        }

                    default:
                        dst[di] = dst[di + 1] = dst[di + 2] = 0;
                        dst[di + 3] = 255;
                        break;
                }
            }
        }

        return dst;
    }

    private static byte[] ResizeBgraNearest(byte[] src, int srcW, int srcH, int dstW, int dstH)
    {
        var dst = new byte[dstW * dstH * 4];
        for (var y = 0; y < dstH; y++)
        {
            var sy = Math.Min(srcH - 1, (int)((y + 0.5) * srcH / dstH));
            for (var x = 0; x < dstW; x++)
            {
                var sx = Math.Min(srcW - 1, (int)((x + 0.5) * srcW / dstW));
                var si = (sy * srcW + sx) * 4;
                var di = (y * dstW + x) * 4;
                dst[di] = src[si];
                dst[di + 1] = src[si + 1];
                dst[di + 2] = src[si + 2];
                dst[di + 3] = src[si + 3];
            }
        }

        return dst;
    }

    private static PdfiumDocument RequirePdfium(IPdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document is not PdfiumDocument pdfium)
        {
            throw new ArgumentException("Document must be a PDFium-backed instance.", nameof(document));
        }

        return pdfium;
    }
}
