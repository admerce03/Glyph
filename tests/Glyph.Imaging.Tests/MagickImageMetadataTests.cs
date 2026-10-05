using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Glyph.Imaging.Magick;
using ImageMagick;

namespace Glyph.Imaging.Tests;

public class MagickImageMetadataTests
{
    [Fact]
    public async Task GetMetadata_reports_dimensions_dpi_and_exif()
    {
        var path = CreateExifJpeg();
        try
        {
            var decoder = new MagickImageDecoder();
            await using var document = await decoder.OpenAsync(path);
            var meta = await document.GetMetadataAsync();

            meta.PixelWidth.Should().Be(40);
            meta.PixelHeight.Should().Be(30);
            meta.FormatName.Should().NotBeNullOrWhiteSpace();
            meta.DpiX.Should().BeApproximately(72, 0.1);
            meta.DpiY.Should().BeApproximately(72, 0.1);
            meta.Make.Should().Be("GlyphCam");
            meta.Model.Should().Be("TestModel");
            meta.Iso.Should().Be("200");
            meta.GpsLatitude.Should().BeApproximately(37.7749, 0.0001);
            meta.GpsLongitude.Should().BeApproximately(-122.4194, 0.0001);
            meta.Entries.Should().Contain(e => e.Group == "EXIF" && e.Name == "Make");
            meta.Entries.Should().Contain(e => e.Group == "GPS" && e.Name == "Latitude");
            meta.FileSizeBytes.Should().NotBeNull();
            meta.FileSizeBytes!.Value.Should().BeGreaterThan(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RemoveGpsMetadata_strips_coordinates_keeps_camera_tags()
    {
        var path = CreateExifJpeg();
        try
        {
            var decoder = new MagickImageDecoder();
            var processor = new MagickImageProcessor();
            await using var document = await decoder.OpenAsync(path);

            await processor.RemoveGpsMetadataAsync(document);
            var meta = await document.GetMetadataAsync();
            meta.GpsLatitude.Should().BeNull();
            meta.GpsLongitude.Should().BeNull();
            meta.Make.Should().Be("GlyphCam");
            meta.Model.Should().Be("TestModel");
            meta.Entries.Should().NotContain(e => e.Group == "GPS");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetMetadata_reports_iptc_title_description_keywords()
    {
        var path = CreateIptcJpeg();
        try
        {
            var decoder = new MagickImageDecoder();
            await using var document = await decoder.OpenAsync(path);
            var meta = await document.GetMetadataAsync();

            meta.HasIptc.Should().BeTrue();
            meta.Title.Should().Be("Glyph Title");
            meta.Description.Should().Be("A test caption");
            meta.Keywords.Should().Contain("alpha");
            meta.Keywords.Should().Contain("beta");
            meta.Copyright.Should().Be("© Glyph");
            meta.Entries.Should().Contain(e => e.Group == "IPTC" && e.Name == "Title");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Set_descriptive_metadata_writes_iptc_fields()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-iptc-write-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var image = new MagickImage(MagickColors.CadetBlue, 24, 18))
            {
                image.Format = MagickFormat.Png;
                await image.WriteAsync(path);
            }

            var decoder = new MagickImageDecoder();
            var processor = new MagickImageProcessor();
            await using var document = await decoder.OpenAsync(path);
            await processor.SetDescriptiveMetadataAsync(
                document,
                new ImageDescriptiveMetadata(
                    Title: "Edited Title",
                    Description: "Edited caption",
                    Keywords: "one, two; three",
                    Copyright: "© Test"));

            var meta = await document.GetMetadataAsync();
            meta.HasIptc.Should().BeTrue();
            meta.Title.Should().Be("Edited Title");
            meta.Description.Should().Be("Edited caption");
            meta.Copyright.Should().Be("© Test");
            meta.Keywords.Should().Contain("one");
            meta.Keywords.Should().Contain("two");
            meta.Keywords.Should().Contain("three");

            await processor.SetDescriptiveMetadataAsync(
                document,
                new ImageDescriptiveMetadata(Title: string.Empty, Description: null, Keywords: "", Copyright: " "));
            var cleared = await document.GetMetadataAsync();
            cleared.Title.Should().BeNullOrEmpty();
            cleared.Description.Should().BeNullOrEmpty();
            cleared.Keywords.Should().BeNullOrEmpty();
            cleared.Copyright.Should().BeNullOrEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAs_can_strip_metadata_while_keeping_pixels()
    {
        var path = CreateExifJpeg();
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-strip-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            var decoder = new MagickImageDecoder();
            var encoder = new MagickImageEncoder();
            await using var document = await decoder.OpenAsync(path);
            var before = await document.GetMetadataAsync();
            before.Make.Should().Be("GlyphCam");

            await encoder.SaveAsAsync(
                document,
                outPath,
                ImageEncodeFormat.Png,
                new ImageEncodeOptions(PreserveMetadata: false));

            await using var reopened = await decoder.OpenAsync(outPath);
            var after = await reopened.GetMetadataAsync();
            after.Make.Should().BeNull();
            after.GpsLatitude.Should().BeNull();
            reopened.PixelWidth.Should().Be(40);
            reopened.PixelHeight.Should().Be(30);
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

    private static string CreateIptcJpeg()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-iptc-" + Guid.NewGuid().ToString("N") + ".jpg");
        using var image = new MagickImage(MagickColors.SteelBlue, 32, 24);
        image.Format = MagickFormat.Jpeg;
        var iptc = new IptcProfile();
        iptc.SetValue(IptcTag.Title, "Glyph Title");
        iptc.SetValue(IptcTag.Caption, "A test caption");
        iptc.SetValue(IptcTag.CopyrightNotice, "© Glyph");
        iptc.SetValue(IptcTag.Keyword, "alpha");
        iptc.SetValue(IptcTag.Keyword, "beta");
        image.SetProfile(iptc);
        image.Write(path);
        return path;
    }

    private static string CreateExifJpeg()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-exif-" + Guid.NewGuid().ToString("N") + ".jpg");
        using var image = new MagickImage(MagickColors.Orange, 40, 30);
        image.Format = MagickFormat.Jpeg;
        image.Density = new Density(72, 72, DensityUnit.PixelsPerInch);

        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "GlyphCam");
        exif.SetValue(ExifTag.Model, "TestModel");
        exif.SetValue(ExifTag.ISOSpeedRatings, new ushort[] { 200 });
        exif.SetValue(ExifTag.DateTimeOriginal, "2026:10:05 12:00:00");
        exif.SetValue(ExifTag.FNumber, new Rational(28, 10));
        exif.SetValue(ExifTag.ExposureTime, new Rational(1, 125));
        exif.SetValue(ExifTag.FocalLength, new Rational(35, 1));
        exif.SetValue(ExifTag.Orientation, (ushort)1);

        // 37°46'29.64" N, 122°25'9.84" W ≈ 37.7749, -122.4194
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(
            ExifTag.GPSLatitude,
            [
                new Rational(37, 1),
                new Rational(46, 1),
                new Rational(2964, 100),
            ]);
        exif.SetValue(ExifTag.GPSLongitudeRef, "W");
        exif.SetValue(
            ExifTag.GPSLongitude,
            [
                new Rational(122, 1),
                new Rational(25, 1),
                new Rational(984, 100),
            ]);

        image.SetProfile(exif);
        image.Write(path);
        return path;
    }
}
