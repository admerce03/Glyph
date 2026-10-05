using System.Runtime.InteropServices;
using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using PDFiumCore;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfiumOptimizeServiceTests
{
    [Fact]
    public async Task Estimate_and_optimize_downsample_high_dpi_image()
    {
        var path = CreateBlankPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var optimize = new PdfiumOptimizeService();
            await using var document = await factory.OpenAsync(path);
            var pdfium = (PdfiumDocument)document;

            PdfiumLibrary.EnsureInitialized();
            lock (PdfiumSync.Gate)
            {
                // 800×800 px image drawn in a 72×72 pt box ≈ 800 DPI.
                InsertSolidImage(pdfium, pageIndex: 0, pixelSize: 800, displayPoints: 72);
            }

            var opts = PdfOptimizeOptions.FromPreset(PdfOptimizePreset.Balanced);
            var estimate = optimize.Estimate(document, opts);
            estimate.ImagesEligibleForDownsample.Should().BeGreaterThan(0);
            estimate.CurrentBytes.Should().BeGreaterThan(0);

            var result = await optimize.OptimizeAsync(document, opts);
            result.ImagesDownsampled.Should().BeGreaterThan(0);
            // SetBitmap may not shrink byte size vs compact JPEG until a JPEG rewrite lands;
            // assert the DPI work happened by checking eligibility drops to zero.
            var again = optimize.Estimate(document, opts);
            again.ImagesEligibleForDownsample.Should().Be(0);
            result.BytesBefore.Should().BeGreaterThan(0);
            result.BytesAfter.Should().BeGreaterThan(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SmallFile_preset_removes_attachments()
    {
        var path = CreateBlankPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var optimize = new PdfiumOptimizeService();
            await using var document = await factory.OpenAsync(path);
            var pdfium = (PdfiumDocument)document;

            PdfiumLibrary.EnsureInitialized();
            lock (PdfiumSync.Gate)
            {
                var nameBytes = System.Text.Encoding.Unicode.GetBytes("blob.bin\0");
                unsafe
                {
                    fixed (byte* namePtr = nameBytes)
                    {
                        var attachment = fpdf_attachment.FPDFDocAddAttachment(pdfium.Handle, ref *(ushort*)namePtr);
                        attachment.Should().NotBeNull();
                        var payload = "payload"u8.ToArray();
                        fixed (byte* data = payload)
                        {
                            fpdf_attachment.FPDFAttachmentSetFile(
                                    attachment,
                                    pdfium.Handle,
                                    (IntPtr)data,
                                    (uint)payload.Length)
                                .Should().NotBe(0);
                        }
                    }
                }
            }

            var result = await optimize.OptimizeAsync(
                document,
                PdfOptimizeOptions.FromPreset(PdfOptimizePreset.SmallFile));
            result.AttachmentsRemoved.Should().Be(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void InsertSolidImage(PdfiumDocument pdfium, int pageIndex, int pixelSize, double displayPoints)
    {
        var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
        page.Should().NotBeNull();
        try
        {
            var pixels = new byte[pixelSize * pixelSize * 4];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 40;     // B
                pixels[i + 1] = 80; // G
                pixels[i + 2] = 160;// R
                pixels[i + 3] = 255;
            }

            var image = fpdf_edit.FPDFPageObjNewImageObj(pdfium.Handle);
            image.Should().NotBeNull();
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            FpdfBitmapT? bitmap = null;
            try
            {
                bitmap = fpdfview.FPDFBitmapCreateEx(
                    pixelSize,
                    pixelSize,
                    PdfiumBitmapFormats.Bgra,
                    handle.AddrOfPinnedObject(),
                    pixelSize * 4);
                bitmap.Should().NotBeNull();
                fpdf_edit.FPDFImageObjSetBitmap(page, 1, image, bitmap).Should().NotBe(0);
                fpdf_edit.FPDFImageObjSetMatrix(
                    image,
                    displayPoints,
                    0,
                    0,
                    displayPoints,
                    72,
                    720 - displayPoints).Should().NotBe(0);
                fpdf_edit.FPDFPageInsertObject(page, image);
                fpdf_edit.FPDFPageGenerateContent(page).Should().NotBe(0);
            }
            finally
            {
                if (bitmap is not null)
                {
                    fpdfview.FPDFBitmapDestroy(bitmap);
                }

                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }
        finally
        {
            fpdfview.FPDF_ClosePage(page);
        }
    }

    private static string CreateBlankPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-opt-" + Guid.NewGuid().ToString("N") + ".pdf");
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.Letter);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }
}
