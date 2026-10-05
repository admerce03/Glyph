using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Glyph.Imaging.Magick;
using ImageMagick;

namespace Glyph.Imaging.Tests;

public class MagickImageMetadataBatchTests
{
    [Fact]
    public async Task Metadata_set_round_trips_title_and_copyright()
    {
        var path = CreatePngWithExif();
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-meta-" + Guid.NewGuid().ToString("N") + ".jpg");
        try
        {
            var decoder = new MagickImageDecoder();
            var meta = new MagickImageMetadataService();
            var encoder = new MagickImageEncoder();
            await using (var document = await decoder.OpenAsync(path))
            {
                var before = await meta.GetAsync(document);
                before.PixelWidth.Should().Be(64);
                before.CameraMake.Should().Be("GlyphCam");

                await meta.SetAsync(document, "Title A", "Desc B", "kw1,kw2", "© Glyph");
                await encoder.SaveAsAsync(document, outPath, ImageEncodeFormat.Jpeg);
            }

            await using var reopened = await decoder.OpenAsync(outPath);
            var after = await meta.GetAsync(reopened);
            after.Title.Should().Be("Title A");
            after.Copyright.Should().Be("© Glyph");
            after.Keywords.Should().Contain("kw1");
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
    public async Task Strip_gps_removes_coordinates()
    {
        var path = CreatePngWithGps();
        try
        {
            var decoder = new MagickImageDecoder();
            var meta = new MagickImageMetadataService();
            await using var document = await decoder.OpenAsync(path);
            var before = await meta.GetAsync(document);
            before.GpsLatitude.Should().NotBeNull();
            before.GpsLongitude.Should().NotBeNull();

            await meta.StripGpsAsync(document);
            var after = await meta.GetAsync(document);
            after.GpsLatitude.Should().BeNull();
            after.GpsLongitude.Should().BeNull();
            after.CameraMake.Should().Be("GlyphCam");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Color_profile_convert_to_srgb_succeeds()
    {
        var path = CreateSolidPng(32, 32);
        try
        {
            var decoder = new MagickImageDecoder();
            var color = new MagickImageColorProfileService();
            await using var document = await decoder.OpenAsync(path);
            var info = await color.GetAsync(document);
            info.ColorSpace.Should().NotBeNullOrWhiteSpace();
            await color.ConvertToSrgbAsync(document);
            var after = await color.GetAsync(document);
            after.ColorSpace.Should().Be("sRGB");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Batch_resize_writes_outputs_with_progress()
    {
        var a = CreateSolidPng(100, 50);
        var b = CreateSolidPng(80, 80);
        var outDir = Path.Combine(Path.GetTempPath(), "glyph-batch-" + Guid.NewGuid().ToString("N"));
        try
        {
            var batch = new MagickImageBatchService(
                new MagickImageDecoder(),
                new MagickImageProcessor(),
                new MagickImageEncoder(),
                new MagickImageMetadataService());
            var reports = new List<ImageBatchProgress>();
            var progress = new Progress<ImageBatchProgress>(reports.Add);
            var result = await batch.RunAsync(
                new ImageBatchRequest(
                    [a, b],
                    outDir,
                    ImageBatchOperationKind.Resize,
                    Width: 40,
                    Height: 20,
                    OutputFormat: ImageEncodeFormat.Png),
                progress);

            result.Succeeded.Should().Be(2);
            result.Failed.Should().Be(0);
            result.OutputPaths.Should().HaveCount(2);
            reports.Should().NotBeEmpty();
            await using var opened = await new MagickImageDecoder().OpenAsync(result.OutputPaths[0]);
            opened.PixelWidth.Should().Be(40);
            opened.PixelHeight.Should().Be(20);
        }
        finally
        {
            File.Delete(a);
            File.Delete(b);
            if (Directory.Exists(outDir))
            {
                Directory.Delete(outDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Emulated_scanner_lists_device_and_writes_png()
    {
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-scan-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            var scanner = new EmulatedScannerService();
            var devices = await scanner.ListDevicesAsync();
            devices.Should().ContainSingle(d => d.IsEmulated);

            await scanner.ScanAsync(new ScanRequest(EmulatedScannerService.EmulatedDeviceId, outPath, Dpi: 72));
            File.Exists(outPath).Should().BeTrue();
            await using var document = await new MagickImageDecoder().OpenAsync(outPath);
            document.PixelWidth.Should().BeGreaterThan(10);
            document.PixelHeight.Should().BeGreaterThan(10);
        }
        finally
        {
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

    private static string CreatePngWithExif()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-exif-" + Guid.NewGuid().ToString("N") + ".jpg");
        using var image = new MagickImage(MagickColors.Orange, 64, 48);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "GlyphCam");
        exif.SetValue(ExifTag.Model, "G1");
        image.SetProfile(exif);
        image.Format = MagickFormat.Jpeg;
        image.Write(path);
        return path;
    }

    private static string CreatePngWithGps()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-gps-" + Guid.NewGuid().ToString("N") + ".jpg");
        using var image = new MagickImage(MagickColors.SeaGreen, 32, 32);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "GlyphCam");
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLatitude, [new Rational(37), new Rational(48), new Rational(0)]);
        exif.SetValue(ExifTag.GPSLongitudeRef, "W");
        exif.SetValue(ExifTag.GPSLongitude, [new Rational(122), new Rational(24), new Rational(0)]);
        image.SetProfile(exif);
        image.Format = MagickFormat.Jpeg;
        image.Write(path);
        return path;
    }
}
