using FluentAssertions;
using Glyph.Imaging.Magick;
using ImageMagick;

namespace Glyph.Imaging.Tests;

public class MagickImageOrientationTests
{
    [Fact]
    public async Task Open_auto_orients_exif_rotated_jpeg()
    {
        var path = CreateOrientedJpeg(orientation: 6, width: 40, height: 20);
        try
        {
            var decoder = new MagickImageDecoder();
            await using var document = await decoder.OpenAsync(path);
            // Orientation 6 = 90° CW → visual size swaps to 20×40.
            document.PixelWidth.Should().Be(20);
            document.PixelHeight.Should().Be(40);

            var meta = await document.GetMetadataAsync();
            // After AutoOrient, orientation is TopLeft / 1 / cleared.
            if (meta.Orientation is not null)
            {
                meta.Orientation.Should().BeOneOf("1", "TopLeft");
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task NormalizeOrientation_is_idempotent_after_open()
    {
        var path = CreateOrientedJpeg(orientation: 6, width: 30, height: 10);
        try
        {
            var decoder = new MagickImageDecoder();
            var processor = new MagickImageProcessor();
            await using var document = await decoder.OpenAsync(path);
            document.PixelWidth.Should().Be(10);
            document.PixelHeight.Should().Be(30);

            await processor.NormalizeOrientationAsync(document);
            document.PixelWidth.Should().Be(10);
            document.PixelHeight.Should().Be(30);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateOrientedJpeg(ushort orientation, int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-orient-" + Guid.NewGuid().ToString("N") + ".jpg");
        using var image = new MagickImage(MagickColors.MediumSeaGreen, (uint)width, (uint)height);
        image.Format = MagickFormat.Jpeg;
        image.Orientation = orientation switch
        {
            3 => OrientationType.BottomRight,
            6 => OrientationType.RightTop,
            8 => OrientationType.LeftBottom,
            _ => OrientationType.TopLeft,
        };
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Orientation, orientation);
        image.SetProfile(exif);
        // Write without baking orientation so OpenAsync must handle it.
        image.Write(path);
        return path;
    }
}
