using System.Runtime.InteropServices;
using Glyph.Pdf.Abstractions;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumRenderer : IPdfRenderer
{
    public Task<PdfRenderResult> RenderPageAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(request);
        if (document is not PdfiumDocument pdfiumDocument)
        {
            throw new ArgumentException("Document must be opened by PdfiumDocumentFactory.", nameof(document));
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();

                lock (PdfiumSync.Gate)
                {
                    pdfiumDocument.ThrowIfDisposed();
                    var pageInfo = (PdfiumPage)pdfiumDocument.GetPage(pageIndex);
                    var scale = request.Scale <= 0 ? 1.0 : request.Scale;

                    var width = (int)Math.Ceiling(pageInfo.WidthPoints * scale);
                    var height = (int)Math.Ceiling(pageInfo.HeightPoints * scale);

                    if (request.MaxWidthPixels is int maxW && width > maxW && width > 0)
                    {
                        var factor = maxW / (double)width;
                        width = maxW;
                        height = Math.Max(1, (int)Math.Ceiling(height * factor));
                        scale = width / pageInfo.WidthPoints;
                    }

                    if (request.MaxHeightPixels is int maxH && height > maxH && height > 0)
                    {
                        var factor = maxH / (double)height;
                        height = maxH;
                        width = Math.Max(1, (int)Math.Ceiling(width * factor));
                        scale = height / pageInfo.HeightPoints;
                    }

                    width = Math.Max(1, width);
                    height = Math.Max(1, height);

                    var page = pageInfo.LoadNativePage();
                    FpdfBitmapT? bitmap = null;
                    try
                    {
                        bitmap = fpdfview.FPDFBitmapCreateEx(
                            width,
                            height,
                            PdfiumDocumentFactory.BitmapBgraFormat,
                            IntPtr.Zero,
                            0);

                        if (bitmap is null)
                        {
                            throw new InvalidOperationException("Failed to create PDFium bitmap.");
                        }

                        // Opaque white background.
                        fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, width, height, 0xFFFFFFFF);

                        using var matrix = new FS_MATRIX_();
                        using var clipping = new FS_RECTF_();
                        matrix.A = (float)scale;
                        matrix.B = 0;
                        matrix.C = 0;
                        matrix.D = (float)scale;
                        matrix.E = 0;
                        matrix.F = 0;
                        clipping.Left = 0;
                        clipping.Right = width;
                        clipping.Bottom = 0;
                        clipping.Top = height;

                        fpdfview.FPDF_RenderPageBitmapWithMatrix(
                            bitmap,
                            page,
                            matrix,
                            clipping,
                            (int)RenderFlags.RenderAnnotations);

                        var stride = fpdfview.FPDFBitmapGetStride(bitmap);
                        var buffer = fpdfview.FPDFBitmapGetBuffer(bitmap);
                        var pixels = new byte[checked(height * width * 4)];

                        // Copy tightly packed BGRA32 (drop any row padding).
                        for (var y = 0; y < height; y++)
                        {
                            Marshal.Copy(buffer + (y * stride), pixels, y * width * 4, width * 4);
                        }

                        return new PdfRenderResult(width, height, pixels);
                    }
                    finally
                    {
                        if (bitmap is not null)
                        {
                            fpdfview.FPDFBitmapDestroy(bitmap);
                        }

                        fpdfview.FPDF_ClosePage(page);
                    }
                }
            },
            cancellationToken);
    }
}
