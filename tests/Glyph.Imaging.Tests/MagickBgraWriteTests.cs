using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Glyph.Imaging.Magick;
using ImageMagick;

namespace Glyph.Imaging.Tests;

public class MagickBgraWriteTests
{
    [Theory]
    [InlineData(ImageEncodeFormat.Png, ".png")]
    [InlineData(ImageEncodeFormat.Jpeg, ".jpg")]
    [InlineData(ImageEncodeFormat.Webp, ".webp")]
    [InlineData(ImageEncodeFormat.Bmp, ".bmp")]
    [InlineData(ImageEncodeFormat.Tiff, ".tif")]
    public async Task WriteBgra_round_trips_dimensions(ImageEncodeFormat format, string extension)
    {
        var width = 48;
        var height = 32;
        var bgra = new byte[width * height * 4];
        for (var i = 0; i < bgra.Length; i += 4)
        {
            bgra[i] = 20;
            bgra[i + 1] = 40;
            bgra[i + 2] = 80;
            bgra[i + 3] = 255;
        }

        var path = Path.Combine(Path.GetTempPath(), "glyph-bgra-" + Guid.NewGuid().ToString("N") + extension);
        try
        {
            var encoder = new MagickImageEncoder();
            await encoder.WriteBgraAsync(
                bgra,
                width,
                height,
                path,
                format,
                format is ImageEncodeFormat.Jpeg or ImageEncodeFormat.Webp
                    ? new ImageEncodeOptions(Quality: 80)
                    : null);

            File.Exists(path).Should().BeTrue();
            using var image = new MagickImage(path);
            image.Width.Should().Be((uint)width);
            image.Height.Should().Be((uint)height);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
