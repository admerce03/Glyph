using System.Runtime.InteropServices;
using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using PDFiumCore;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfiumOptimizeServiceTests
{
    [Fact]
    public async Task Lower_jpeg_quality_yields_smaller_image_stream()
    {
        var pathLow = CreateBlankPdf();
        var pathHigh = CreateBlankPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var lowEnc = new RecordingJpegEncoder();
            var highEnc = new RecordingJpegEncoder();

            async Task<uint> OptimizeAndRawLenAsync(string path, int quality, RecordingJpegEncoder encoder)
            {
                var optimize = new PdfiumOptimizeService(encoder);
                await using var document = await factory.OpenAsync(path);
                var pdfium = (PdfiumDocument)document;
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    InsertPatternedImage(pdfium, pageIndex: 0, pixelSize: 800, displayPoints: 72);
                }

                var opts = PdfOptimizeOptions.FromPreset(PdfOptimizePreset.Balanced) with { JpegQuality = quality };
                var result = await optimize.OptimizeAsync(document, opts);
                result.ImagesDownsampled.Should().BeGreaterThan(0);
                encoder.LastQuality.Should().Be(quality);
                encoder.LastJpegLength.Should().BeGreaterThan(0);

                lock (PdfiumSync.Gate)
                {
                    var page = fpdfview.FPDF_LoadPage(pdfium.Handle, 0);
                    page.Should().NotBeNull();
                    try
                    {
                        FpdfPageobjectT? image = null;
                        var count = fpdf_edit.FPDFPageCountObjects(page);
                        for (var i = 0; i < count; i++)
                        {
                            var obj = fpdf_edit.FPDFPageGetObject(page, i);
                            if (obj is not null && fpdf_edit.FPDFPageObjGetType(obj) == 3)
                            {
                                image = obj;
                                break;
                            }
                        }

                        image.Should().NotBeNull();
                        var filters = fpdf_edit.FPDFImageObjGetImageFilterCount(image);
                        filters.Should().BeGreaterThan(0, "expected DCTDecode JPEG rewrite, not SetBitmap fallback");
                        var flen = fpdf_edit.FPDFImageObjGetImageFilter(image, 0, IntPtr.Zero, 0);
                        var fbuf = new byte[flen];
                        var fh = GCHandle.Alloc(fbuf, GCHandleType.Pinned);
                        try
                        {
                            fpdf_edit.FPDFImageObjGetImageFilter(image, 0, fh.AddrOfPinnedObject(), (uint)fbuf.Length);
                            var n = Array.IndexOf(fbuf, (byte)0);
                            var name = System.Text.Encoding.ASCII.GetString(fbuf, 0, n < 0 ? fbuf.Length : n);
                            name.Should().Be("DCTDecode");
                        }
                        finally
                        {
                            fh.Free();
                        }

                        return fpdf_edit.FPDFImageObjGetImageDataRaw(image, IntPtr.Zero, 0);
                    }
                    finally
                    {
                        fpdfview.FPDF_ClosePage(page);
                    }
                }
            }

            var lowLen = await OptimizeAndRawLenAsync(pathLow, quality: 30, lowEnc);
            var highLen = await OptimizeAndRawLenAsync(pathHigh, quality: 90, highEnc);
            lowEnc.LastJpegLength.Should().BeLessThan(highEnc.LastJpegLength);
            lowLen.Should().Be((uint)lowEnc.LastJpegLength);
            highLen.Should().Be((uint)highEnc.LastJpegLength);
            lowLen.Should().BeLessThan(highLen);
        }
        finally
        {
            File.Delete(pathLow);
            File.Delete(pathHigh);
        }
    }

    [Fact]
    public async Task Estimate_lower_jpeg_quality_predicts_smaller_output()
    {
        var path = CreateBlankPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var optimize = new PdfiumOptimizeService(new MagickTestJpegEncoder());
            await using var document = await factory.OpenAsync(path);
            var pdfium = (PdfiumDocument)document;

            PdfiumLibrary.EnsureInitialized();
            lock (PdfiumSync.Gate)
            {
                InsertPatternedImage(pdfium, pageIndex: 0, pixelSize: 800, displayPoints: 72);
            }

            var high = optimize.Estimate(
                document,
                PdfOptimizeOptions.FromPreset(PdfOptimizePreset.Balanced) with { JpegQuality = 90 });
            var low = optimize.Estimate(
                document,
                PdfOptimizeOptions.FromPreset(PdfOptimizePreset.Balanced) with { JpegQuality = 30 });
            high.ImagesEligibleForDownsample.Should().BeGreaterThan(0);
            low.ImagesEligibleForDownsample.Should().Be(high.ImagesEligibleForDownsample);
            low.EstimatedBytes.Should().BeLessThan(high.EstimatedBytes);
            low.EstimatedBytes.Should().BeLessThanOrEqualTo(low.CurrentBytes);
        }
        finally
        {
            File.Delete(path);
        }
    }

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
            // SetBitmap path may not shrink bytes vs compact sources; assert DPI work via eligibility.
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
    public async Task Optimize_with_jpeg_encoder_writes_dctdecode()
    {
        var path = CreateBlankPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var optimize = new PdfiumOptimizeService(new MagickTestJpegEncoder());
            await using var document = await factory.OpenAsync(path);
            var pdfium = (PdfiumDocument)document;

            PdfiumLibrary.EnsureInitialized();
            lock (PdfiumSync.Gate)
            {
                // High-DPI solid image so Balanced (150 DPI target) downsamples.
                InsertSolidImage(pdfium, pageIndex: 0, pixelSize: 800, displayPoints: 72);
            }

            var opts = PdfOptimizeOptions.FromPreset(PdfOptimizePreset.Balanced);
            opts = opts with { JpegQuality = 40 };
            var result = await optimize.OptimizeAsync(document, opts);
            result.ImagesDownsampled.Should().BeGreaterThan(0);

            PdfiumLibrary.EnsureInitialized();
            lock (PdfiumSync.Gate)
            {
                var page = fpdfview.FPDF_LoadPage(pdfium.Handle, 0);
                page.Should().NotBeNull();
                try
                {
                    var count = fpdf_edit.FPDFPageCountObjects(page);
                    count.Should().BeGreaterThan(0);
                    FpdfPageobjectT? image = null;
                    for (var i = 0; i < count; i++)
                    {
                        var obj = fpdf_edit.FPDFPageGetObject(page, i);
                        if (obj is not null && fpdf_edit.FPDFPageObjGetType(obj) == 3)
                        {
                            image = obj;
                            break;
                        }
                    }

                    image.Should().NotBeNull();
                    var filters = fpdf_edit.FPDFImageObjGetImageFilterCount(image);
                    filters.Should().BeGreaterThan(0);
                    var len = fpdf_edit.FPDFImageObjGetImageFilter(image, 0, IntPtr.Zero, 0);
                    var buf = new byte[len];
                    var handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
                    try
                    {
                        fpdf_edit.FPDFImageObjGetImageFilter(image, 0, handle.AddrOfPinnedObject(), (uint)buf.Length);
                        var n = Array.IndexOf(buf, (byte)0);
                        var name = System.Text.Encoding.ASCII.GetString(buf, 0, n < 0 ? buf.Length : n);
                        name.Should().Be("DCTDecode");
                    }
                    finally
                    {
                        handle.Free();
                    }

                    var rawLen = fpdf_edit.FPDFImageObjGetImageDataRaw(image, IntPtr.Zero, 0);
                    rawLen.Should().BeGreaterThan(0);
                    var raw = new byte[rawLen];
                    var rh = GCHandle.Alloc(raw, GCHandleType.Pinned);
                    try
                    {
                        fpdf_edit.FPDFImageObjGetImageDataRaw(image, rh.AddrOfPinnedObject(), rawLen);
                        raw[0].Should().Be(0xFF);
                        raw[1].Should().Be(0xD8);
                    }
                    finally
                    {
                        rh.Free();
                    }
                }
                finally
                {
                    fpdfview.FPDF_ClosePage(page);
                }
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromPreset_HighQuality_targets_200dpi_above_300()
    {
        var opts = PdfOptimizeOptions.FromPreset(PdfOptimizePreset.HighQuality);
        opts.DownsampleImages.Should().BeTrue();
        opts.DownsampleAboveDpi.Should().Be(300);
        opts.TargetDpi.Should().Be(200);
        opts.JpegQuality.Should().Be(85);
        opts.RemoveEmbeddedAttachments.Should().BeFalse();
        opts.PreserveMonochrome.Should().BeTrue();
    }

    [Fact]
    public void FromPreset_Balanced_targets_150dpi_above_225()
    {
        var opts = PdfOptimizeOptions.FromPreset(PdfOptimizePreset.Balanced);
        opts.DownsampleImages.Should().BeTrue();
        opts.DownsampleAboveDpi.Should().Be(225);
        opts.TargetDpi.Should().Be(150);
        opts.JpegQuality.Should().Be(75);
        opts.RemoveEmbeddedAttachments.Should().BeFalse();
        opts.RemoveMetadata.Should().BeFalse();
    }

    [Fact]
    public void FromPreset_SmallFile_strips_attachments_and_metadata()
    {
        var opts = PdfOptimizeOptions.FromPreset(PdfOptimizePreset.SmallFile);
        opts.DownsampleImages.Should().BeTrue();
        opts.DownsampleAboveDpi.Should().Be(150);
        opts.TargetDpi.Should().Be(96);
        opts.JpegQuality.Should().Be(55);
        opts.RemoveEmbeddedAttachments.Should().BeTrue();
        opts.RemoveMetadata.Should().BeTrue();
    }

    [Fact]
    public void FromPreset_Lossless_disables_downsample()
    {
        var opts = PdfOptimizeOptions.FromPreset(PdfOptimizePreset.Lossless);
        opts.DownsampleImages.Should().BeFalse();
        opts.RemoveEmbeddedAttachments.Should().BeFalse();
        opts.RemoveMetadata.Should().BeFalse();
    }

    [Fact]
    public void Custom_options_preserve_explicit_fields()
    {
        var opts = new PdfOptimizeOptions(
            Preset: PdfOptimizePreset.Custom,
            DownsampleImages: true,
            DownsampleAboveDpi: 400,
            TargetDpi: 120,
            JpegQuality: 42,
            PreserveMonochrome: false,
            RemoveEmbeddedAttachments: true,
            RemoveMetadata: true);

        opts.Preset.Should().Be(PdfOptimizePreset.Custom);
        opts.DownsampleAboveDpi.Should().Be(400);
        opts.TargetDpi.Should().Be(120);
        opts.JpegQuality.Should().Be(42);
        opts.PreserveMonochrome.Should().BeFalse();
        opts.RemoveEmbeddedAttachments.Should().BeTrue();
        opts.RemoveMetadata.Should().BeTrue();
    }

    [Fact]
    public async Task Custom_RemoveMetadata_clears_info_fields()
    {
        var path = CreateInfoPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var optimize = new PdfiumOptimizeService();
            var infoService = new PdfiumDocumentInfoService();
            await using var document = await factory.OpenAsync(path);

            infoService.GetInfo(document).Title.Should().Be("Glyph Title");

            var result = await optimize.OptimizeAsync(
                document,
                new PdfOptimizeOptions(
                    Preset: PdfOptimizePreset.Custom,
                    DownsampleImages: false,
                    RemoveMetadata: true));
            result.BytesBefore.Should().BeGreaterThan(0);

            var after = infoService.GetInfo(document);
            after.Title.Should().BeNullOrEmpty();
            after.Author.Should().BeNullOrEmpty();
            after.Creator.Should().BeNullOrEmpty();
            after.Producer.Should().BeNullOrEmpty();
            after.CreationDate.Should().BeNullOrEmpty();
            after.ModificationDate.Should().BeNullOrEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task HighQuality_downsamples_images_above_300dpi()
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
                // 800px in 72pt ≈ 800 DPI → above HighQuality's 300 threshold.
                InsertSolidImage(pdfium, pageIndex: 0, pixelSize: 800, displayPoints: 72);
            }

            var result = await optimize.OptimizeAsync(
                document,
                PdfOptimizeOptions.FromPreset(PdfOptimizePreset.HighQuality));
            result.ImagesDownsampled.Should().BeGreaterThan(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PreserveMonochrome_false_still_downsamples_color_images()
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
                InsertSolidImage(pdfium, pageIndex: 0, pixelSize: 800, displayPoints: 72);
            }

            var result = await optimize.OptimizeAsync(
                document,
                PdfOptimizeOptions.FromPreset(PdfOptimizePreset.Balanced) with { PreserveMonochrome = false });
            result.ImagesDownsampled.Should().BeGreaterThan(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Lossless_full_rewrite_succeeds()
    {
        var path = CreateBlankPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var optimize = new PdfiumOptimizeService();
            await using var document = await factory.OpenAsync(path);

            var result = await optimize.OptimizeAsync(
                document,
                PdfOptimizeOptions.FromPreset(PdfOptimizePreset.Lossless));
            result.ImagesDownsampled.Should().Be(0);
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
        InsertImage(pdfium, pageIndex, pixelSize, displayPoints, solid: true);
    }

    private static void InsertPatternedImage(PdfiumDocument pdfium, int pageIndex, int pixelSize, double displayPoints)
    {
        InsertImage(pdfium, pageIndex, pixelSize, displayPoints, solid: false);
    }

    private static void InsertImage(PdfiumDocument pdfium, int pageIndex, int pixelSize, double displayPoints, bool solid)
    {
        var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
        page.Should().NotBeNull();
        try
        {
            var pixels = new byte[pixelSize * pixelSize * 4];
            for (var y = 0; y < pixelSize; y++)
            {
                for (var x = 0; x < pixelSize; x++)
                {
                    var i = (y * pixelSize + x) * 4;
                    if (solid)
                    {
                        pixels[i] = 40;
                        pixels[i + 1] = 80;
                        pixels[i + 2] = 160;
                    }
                    else
                    {
                        // Spatial noise so JPEG quality materially changes stream size.
                        pixels[i] = (byte)((x * 37 + y * 17) & 0xFF);
                        pixels[i + 1] = (byte)((x * 13 + y * 53) & 0xFF);
                        pixels[i + 2] = (byte)((x * 91 + y * 7) & 0xFF);
                    }

                    pixels[i + 3] = 255;
                }
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

    private static string CreateInfoPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-opt-info-" + Guid.NewGuid().ToString("N") + ".pdf");
        var builder = new PdfDocumentBuilder();
        builder.DocumentInformation.Title = "Glyph Title";
        builder.DocumentInformation.Author = "Glyph Author";
        builder.DocumentInformation.Subject = "Glyph Subject";
        builder.DocumentInformation.Keywords = "glyph, test";
        builder.DocumentInformation.Creator = "Glyph Creator";
        builder.DocumentInformation.Producer = "Glyph Producer";
        builder.AddPage(PageSize.Letter);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    private sealed class RecordingJpegEncoder : IPdfImageJpegEncoder
    {
        private readonly MagickTestJpegEncoder _inner = new();
        public int LastQuality { get; private set; }
        public int LastJpegLength { get; private set; }

        public byte[]? EncodeBgraToJpeg(ReadOnlySpan<byte> bgra, int width, int height, int quality)
        {
            LastQuality = quality;
            var jpeg = _inner.EncodeBgraToJpeg(bgra, width, height, quality);
            LastJpegLength = jpeg?.Length ?? 0;
            return jpeg;
        }
    }

    private sealed class MagickTestJpegEncoder : IPdfImageJpegEncoder
    {
        public byte[]? EncodeBgraToJpeg(ReadOnlySpan<byte> bgra, int width, int height, int quality)
        {
            if (width <= 0 || height <= 0 || bgra.Length < width * height * 4)
            {
                return null;
            }

            quality = Math.Clamp(quality, 1, 100);
            var copy = bgra.ToArray();
            using var image = new ImageMagick.MagickImage();
            image.ReadPixels(
                copy,
                new ImageMagick.PixelReadSettings(
                    (uint)width,
                    (uint)height,
                    ImageMagick.StorageType.Char,
                    ImageMagick.PixelMapping.BGRA));
            image.Format = ImageMagick.MagickFormat.Jpeg;
            image.Quality = (uint)quality;
            image.Alpha(ImageMagick.AlphaOption.Remove);
            return image.ToByteArray();
        }
    }
}
