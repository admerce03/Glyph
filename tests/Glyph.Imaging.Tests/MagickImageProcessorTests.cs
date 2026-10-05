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

    private static string CreateSolidPng(int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-src-" + Guid.NewGuid().ToString("N") + ".png");
        using var image = new MagickImage(MagickColors.DodgerBlue, (uint)width, (uint)height);
        image.Format = MagickFormat.Png;
        image.Write(path);
        return path;
    }
}
