using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Glyph.Imaging.Magick;
using ImageMagick;

namespace Glyph.Imaging.Tests;

public class MagickImageDecoderFormatTests
{
    [Theory]
    [InlineData(ImageEncodeFormat.Avif, ".avif")]
    [InlineData(ImageEncodeFormat.Jpeg2000, ".jp2")]
    [InlineData(ImageEncodeFormat.Webp, ".webp")]
    public async Task OpenAsync_reads_encoded_formats(ImageEncodeFormat format, string extension)
    {
        var width = 40;
        var height = 24;
        var bgra = new byte[width * height * 4];
        for (var i = 0; i < bgra.Length; i += 4)
        {
            bgra[i] = 10;
            bgra[i + 1] = 80;
            bgra[i + 2] = 160;
            bgra[i + 3] = 255;
        }

        var path = Path.Combine(Path.GetTempPath(), "glyph-decode-" + Guid.NewGuid().ToString("N") + extension);
        try
        {
            var encoder = new MagickImageEncoder();
            await encoder.WriteBgraAsync(
                bgra,
                width,
                height,
                path,
                format,
                format is ImageEncodeFormat.Avif or ImageEncodeFormat.Webp
                    ? new ImageEncodeOptions(Quality: 75)
                    : null);

            var decoder = new MagickImageDecoder();
            await using var document = await decoder.OpenAsync(path);
            document.PixelWidth.Should().Be(width);
            document.PixelHeight.Should().Be(height);
            var pixels = await document.GetPixelsAsync();
            pixels.Width.Should().Be(width);
            pixels.Height.Should().Be(height);
            pixels.BgraPixels.Length.Should().Be(width * height * 4);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task OpenAsync_reads_heic_when_codec_available()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-heic-" + Guid.NewGuid().ToString("N") + ".heic");
        try
        {
            try
            {
                using var image = new MagickImage(MagickColors.DodgerBlue, 32, 20);
                image.Format = MagickFormat.Heic;
                await image.WriteAsync(path);
            }
            catch (MagickException)
            {
                // Magick build without HEIC encode — skip write/open round-trip.
                return;
            }

            var decoder = new MagickImageDecoder();
            await using var document = await decoder.OpenAsync(path);
            document.PixelWidth.Should().Be(32);
            document.PixelHeight.Should().Be(20);
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
