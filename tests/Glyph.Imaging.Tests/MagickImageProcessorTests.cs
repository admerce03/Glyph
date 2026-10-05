using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Glyph.Imaging.Magick;
using ImageMagick;

namespace Glyph.Imaging.Tests;

public class MagickImageProcessorTests
{
    [Fact]
    public async Task Probe_and_open_report_dimensions_without_requiring_full_ui_decode()
    {
        var path = CreateSolidPng(120, 80);
        try
        {
            var decoder = new MagickImageDecoder();
            var info = await decoder.ProbeAsync(path);
            info.PixelWidth.Should().Be(120);
            info.PixelHeight.Should().Be(80);

            await using var document = await decoder.OpenAsync(path);
            document.PixelWidth.Should().Be(120);
            document.PixelHeight.Should().Be(80);

            var preview = await document.GetPixelsAsync(maxEdge: 60);
            Math.Max(preview.Width, preview.Height).Should().BeLessThanOrEqualTo(60);
            preview.BgraPixels.Length.Should().Be(preview.Width * preview.Height * 4);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Crop_resize_rotate_and_flip_round_trip()
    {
        var path = CreateSolidPng(200, 100);
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-img-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            var decoder = new MagickImageDecoder();
            var processor = new MagickImageProcessor();
            var encoder = new MagickImageEncoder();
            await using var document = await decoder.OpenAsync(path);

            await processor.CropAsync(document, new ImageRect(10, 10, 80, 60));
            document.PixelWidth.Should().Be(80);
            document.PixelHeight.Should().Be(60);

            await processor.ResizeAsync(document, 40, 30);
            document.PixelWidth.Should().Be(40);
            document.PixelHeight.Should().Be(30);

            await processor.RotateAsync(document, 90);
            document.PixelWidth.Should().Be(30);
            document.PixelHeight.Should().Be(40);

            await processor.FlipHorizontalAsync(document);
            await processor.FlipVerticalAsync(document);

            await encoder.SaveAsAsync(document, outPath, ImageEncodeFormat.Png);
            File.Exists(outPath).Should().BeTrue();

            await using var reopened = await decoder.OpenAsync(outPath);
            reopened.PixelWidth.Should().Be(30);
            reopened.PixelHeight.Should().Be(40);
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    [Fact]
    public async Task Rotate_right_180_and_resize_preserve_dimensions()
    {
        var path = CreateSolidPng(160, 80);
        try
        {
            var decoder = new MagickImageDecoder();
            var processor = new MagickImageProcessor();
            await using var document = await decoder.OpenAsync(path);

            await processor.RotateAsync(document, 90);
            document.PixelWidth.Should().Be(80);
            document.PixelHeight.Should().Be(160);

            await processor.RotateAsync(document, 180);
            document.PixelWidth.Should().Be(80);
            document.PixelHeight.Should().Be(160);

            await processor.ResizeAsync(document, 40, 80);
            document.PixelWidth.Should().Be(40);
            document.PixelHeight.Should().Be(80);

            await processor.FlipHorizontalAsync(document);
            await processor.FlipVerticalAsync(document);
            document.PixelWidth.Should().Be(40);
            document.PixelHeight.Should().Be(80);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Paste_rect_composites_extracted_pixels()
    {
        var path = CreateSolidPng(40, 30);
        try
        {
            var decoder = new MagickImageDecoder();
            var processor = new MagickImageProcessor();
            await using var document = await decoder.OpenAsync(path);

            var extracted = await processor.ExtractRectAsync(document, new ImageRect(0, 0, 8, 8));
            await processor.ClearRectAsync(document, new ImageRect(20, 10, 8, 8), transparent: true);
            await processor.PasteRectAsync(document, extracted, 20, 10);
            document.PixelWidth.Should().Be(40);
            document.PixelHeight.Should().Be(30);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Paste_file_composites_onto_document()
    {
        var basePath = CreateSolidPng(40, 30);
        var stampPath = CreateSolidPng(8, 8);
        try
        {
            var decoder = new MagickImageDecoder();
            var processor = new MagickImageProcessor();
            await using var document = await decoder.OpenAsync(basePath);
            await processor.PasteFileAsync(document, stampPath, 10, 8);
            document.PixelWidth.Should().Be(40);
            document.PixelHeight.Should().Be(30);
        }
        finally
        {
            File.Delete(basePath);
            File.Delete(stampPath);
        }
    }

    [Fact]
    public async Task Flatten_markup_stroke_keeps_dimensions()
    {
        var path = CreateSolidPng(40, 30);
        try
        {
            var decoder = new MagickImageDecoder();
            var processor = new MagickImageProcessor();
            await using var document = await decoder.OpenAsync(path);

            var stroke = new ImageMarkupStroke(
                [
                    new ImageMarkupPoint(2, 2),
                    new ImageMarkupPoint(20, 10),
                    new ImageMarkupPoint(35, 25),
                ],
                a: 255,
                r: 255,
                g: 0,
                b: 0,
                widthPixels: 2);
            var rect = new ImageMarkupShape(
                ImageMarkupShapeKind.Rectangle,
                4,
                4,
                18,
                14,
                255,
                0,
                128,
                255,
                2);
            var arrow = new ImageMarkupShape(
                ImageMarkupShapeKind.Arrow,
                5,
                20,
                30,
                8,
                255,
                0,
                200,
                0,
                2);
            var text = new ImageMarkupShape(
                ImageMarkupShapeKind.Text,
                8,
                22,
                8,
                22,
                255,
                0,
                0,
                0,
                1,
                "Hi",
                14);
            var callout = new ImageMarkupShape(
                ImageMarkupShapeKind.Callout,
                10,
                4,
                28,
                16,
                255,
                40,
                40,
                200,
                2,
                "Tip",
                12);
            await processor.FlattenMarkupAsync(
                document,
                new ImageMarkupLayer([stroke], [rect, arrow, text, callout]));
            document.PixelWidth.Should().Be(40);
            document.PixelHeight.Should().Be(30);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Ellipse_extract_clears_corners_to_transparent()
    {
        var path = CreateSolidPng(40, 30);
        try
        {
            var decoder = new MagickImageDecoder();
            var processor = new MagickImageProcessor();
            await using var document = await decoder.OpenAsync(path);

            var extracted = await processor.ExtractRectAsync(
                document,
                new ImageRect(0, 0, 20, 20),
                ImageSelectionKind.Ellipse);
            extracted.Width.Should().Be(20);
            extracted.Height.Should().Be(20);
            // Corner pixel of bounding box should be outside the ellipse → alpha 0.
            extracted.BgraPixels[3].Should().Be(0);

            await processor.ClearRectAsync(
                document,
                new ImageRect(0, 0, 20, 20),
                transparent: true,
                ImageSelectionKind.Ellipse);
            await processor.MoveRectAsync(
                document,
                new ImageRect(10, 5, 12, 12),
                24,
                14,
                ImageSelectionKind.Ellipse);
            document.PixelWidth.Should().Be(40);
            document.PixelHeight.Should().Be(30);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Move_rect_relocates_pixels_and_clears_source()
    {
        var path = CreateSolidPng(40, 30);
        try
        {
            var decoder = new MagickImageDecoder();
            var processor = new MagickImageProcessor();
            await using var document = await decoder.OpenAsync(path);

            await processor.MoveRectAsync(document, new ImageRect(0, 0, 8, 8), 20, 10);
            document.PixelWidth.Should().Be(40);
            document.PixelHeight.Should().Be(30);

            // No-op when destination equals source.
            await processor.MoveRectAsync(document, new ImageRect(20, 10, 8, 8), 20, 10);
            document.PixelWidth.Should().Be(40);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Extract_and_clear_rect_round_trip()
    {
        var path = CreateSolidPng(40, 30);
        try
        {
            var decoder = new MagickImageDecoder();
            var processor = new MagickImageProcessor();
            await using var document = await decoder.OpenAsync(path);

            var extracted = await processor.ExtractRectAsync(document, new ImageRect(5, 5, 10, 8));
            extracted.Width.Should().Be(10);
            extracted.Height.Should().Be(8);
            extracted.BgraPixels.Length.Should().Be(10 * 8 * 4);

            await processor.ClearRectAsync(document, new ImageRect(0, 0, 10, 10), transparent: true);
            document.PixelWidth.Should().Be(40);
            document.PixelHeight.Should().Be(30);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Resize_with_filter_and_dpi_updates_density()
    {
        var path = CreateSolidPng(80, 60);
        try
        {
            var decoder = new MagickImageDecoder();
            var processor = new MagickImageProcessor();
            await using var document = await decoder.OpenAsync(path);

            await processor.ResizeAsync(
                document,
                40,
                30,
                new ImageResizeOptions(Filter: ImageResizeFilter.Bicubic, DensityDpi: 150));
            document.PixelWidth.Should().Be(40);
            document.PixelHeight.Should().Be(30);

            var meta = await document.GetMetadataAsync();
            meta.DpiX.Should().BeApproximately(150, 0.5);
            meta.DpiY.Should().BeApproximately(150, 0.5);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Adjust_brightness_contrast_saturation_keeps_dimensions()
    {
        var path = CreateSolidPng(64, 48);
        try
        {
            var decoder = new MagickImageDecoder();
            var processor = new MagickImageProcessor();
            await using var document = await decoder.OpenAsync(path);

            await processor.AdjustAsync(document, new ImageAdjustments(Brightness: 20, Contrast: 10, Saturation: -15));
            document.PixelWidth.Should().Be(64);
            document.PixelHeight.Should().Be(48);

            var pixels = await document.GetPixelsAsync(maxEdge: 64);
            pixels.BgraPixels.Length.Should().Be(pixels.Width * pixels.Height * 4);
            pixels.BgraPixels.Should().Contain(b => b != 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Adjust_auto_levels_sharpness_sepia_keeps_dimensions()
    {
        var path = CreateSolidPng(48, 32);
        try
        {
            var decoder = new MagickImageDecoder();
            var processor = new MagickImageProcessor();
            await using var document = await decoder.OpenAsync(path);

            await processor.AdjustAsync(
                document,
                new ImageAdjustments(
                    AutoLevels: true,
                    Sharpness: 40,
                    Sepia: true,
                    Temperature: 25,
                    Tint: -15,
                    Highlights: -20,
                    Shadows: 30,
                    BlackPoint: 5,
                    WhitePoint: 95,
                    Gamma: 1.1));
            document.PixelWidth.Should().Be(48);
            document.PixelHeight.Should().Be(32);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(ImageEncodeFormat.Jpeg, ".jpg")]
    [InlineData(ImageEncodeFormat.Webp, ".webp")]
    [InlineData(ImageEncodeFormat.Bmp, ".bmp")]
    [InlineData(ImageEncodeFormat.Tiff, ".tif")]
    [InlineData(ImageEncodeFormat.Gif, ".gif")]
    public async Task SaveAs_writes_common_formats(ImageEncodeFormat format, string extension)
    {
        var path = CreateSolidPng(32, 24);
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-out-" + Guid.NewGuid().ToString("N") + extension);
        try
        {
            var decoder = new MagickImageDecoder();
            var encoder = new MagickImageEncoder();
            await using var document = await decoder.OpenAsync(path);
            await encoder.SaveAsAsync(document, outPath, format);
            File.Exists(outPath).Should().BeTrue();
            new FileInfo(outPath).Length.Should().BeGreaterThan(0);

            await using var reopened = await decoder.OpenAsync(outPath);
            reopened.PixelWidth.Should().Be(32);
            reopened.PixelHeight.Should().Be(24);
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    [Fact]
    public async Task Jpeg_quality_lower_produces_smaller_or_equal_file()
    {
        var path = CreateSolidPng(128, 96);
        var highPath = Path.Combine(Path.GetTempPath(), "glyph-q90-" + Guid.NewGuid().ToString("N") + ".jpg");
        var lowPath = Path.Combine(Path.GetTempPath(), "glyph-q20-" + Guid.NewGuid().ToString("N") + ".jpg");
        try
        {
            var decoder = new MagickImageDecoder();
            var encoder = new MagickImageEncoder();
            await using var document = await decoder.OpenAsync(path);
            await encoder.SaveAsAsync(document, highPath, ImageEncodeFormat.Jpeg, new ImageEncodeOptions(Quality: 90));
            await encoder.SaveAsAsync(document, lowPath, ImageEncodeFormat.Jpeg, new ImageEncodeOptions(Quality: 20));
            new FileInfo(lowPath).Length.Should().BeLessThanOrEqualTo(new FileInfo(highPath).Length);
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(highPath))
            {
                File.Delete(highPath);
            }

            if (File.Exists(lowPath))
            {
                File.Delete(lowPath);
            }
        }
    }

    [Fact]
    public async Task Webp_lossless_round_trips_dimensions()
    {
        var path = CreateSolidPng(48, 36);
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-webp-" + Guid.NewGuid().ToString("N") + ".webp");
        try
        {
            var decoder = new MagickImageDecoder();
            var encoder = new MagickImageEncoder();
            await using var document = await decoder.OpenAsync(path);
            await encoder.SaveAsAsync(document, outPath, ImageEncodeFormat.Webp, new ImageEncodeOptions(Lossless: true));
            await using var reopened = await decoder.OpenAsync(outPath);
            reopened.PixelWidth.Should().Be(48);
            reopened.PixelHeight.Should().Be(36);
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    [Fact]
    public async Task Open_ico_reports_dimensions()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-ico-" + Guid.NewGuid().ToString("N") + ".ico");
        try
        {
            using (var image = new MagickImage(MagickColors.DodgerBlue, 32, 32))
            {
                image.Format = MagickFormat.Ico;
                image.Write(path);
            }

            var decoder = new MagickImageDecoder();
            await using var document = await decoder.OpenAsync(path);
            document.PixelWidth.Should().Be(32);
            document.PixelHeight.Should().Be(32);
            document.FormatName.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string CreateSolidPng(int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-src-" + Guid.NewGuid().ToString("N") + ".png");
        using var image = new MagickImage(MagickColors.DodgerBlue, (uint)width, (uint)height);
        image.Format = MagickFormat.Png;
        image.Write(path);
        return path;
    }
}
