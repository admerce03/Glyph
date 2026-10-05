using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Xunit;

namespace Glyph.Imaging.Tests;

public class ImageEncodeFormatResolverTests
{
    [Theory]
    [InlineData("JPEG", ImageEncodeFormat.Jpeg, ".jpg")]
    [InlineData("Jpg", ImageEncodeFormat.Jpeg, ".jpg")]
    [InlineData("WebP", ImageEncodeFormat.Webp, ".webp")]
    [InlineData("TIFF", ImageEncodeFormat.Tiff, ".tif")]
    [InlineData("BMP", ImageEncodeFormat.Bmp, ".bmp")]
    [InlineData("GIF", ImageEncodeFormat.Gif, ".gif")]
    [InlineData("AVIF", ImageEncodeFormat.Avif, ".avif")]
    [InlineData("JPEG 2000", ImageEncodeFormat.Jpeg2000, ".jp2")]
    [InlineData("Jp2", ImageEncodeFormat.Jpeg2000, ".jp2")]
    [InlineData("Jpeg2000", ImageEncodeFormat.Jpeg2000, ".jp2")]
    [InlineData("HEIC", ImageEncodeFormat.Heic, ".heic")]
    [InlineData("HEIF", ImageEncodeFormat.Heic, ".heic")]
    [InlineData("PNG", ImageEncodeFormat.Png, ".png")]
    [InlineData("PDF", ImageEncodeFormat.Pdf, ".pdf")]
    [InlineData("UnknownCodec", ImageEncodeFormat.Png, ".png")]
    [InlineData(null, ImageEncodeFormat.Png, ".png")]
    public void FromFormatName_maps_codecs(string? name, ImageEncodeFormat format, string extension)
    {
        ImageEncodeFormatResolver.FromFormatName(name).Should().Be((format, extension));
    }

    [Theory]
    [InlineData(".jpg", ImageEncodeFormat.Jpeg)]
    [InlineData("jpeg", ImageEncodeFormat.Jpeg)]
    [InlineData(".WEBP", ImageEncodeFormat.Webp)]
    [InlineData(".tif", ImageEncodeFormat.Tiff)]
    [InlineData(".tiff", ImageEncodeFormat.Tiff)]
    [InlineData(".avif", ImageEncodeFormat.Avif)]
    [InlineData(".jp2", ImageEncodeFormat.Jpeg2000)]
    [InlineData(".j2k", ImageEncodeFormat.Jpeg2000)]
    [InlineData(".heic", ImageEncodeFormat.Heic)]
    [InlineData(".heif", ImageEncodeFormat.Heic)]
    [InlineData(".png", ImageEncodeFormat.Png)]
    [InlineData(".xyz", ImageEncodeFormat.Png)]
    public void FromExtension_maps_codecs(string extension, ImageEncodeFormat format)
    {
        ImageEncodeFormatResolver.FromExtension(extension).Should().Be(format);
    }

    [Fact]
    public void ExtensionFor_matches_from_format_name()
    {
        foreach (ImageEncodeFormat format in Enum.GetValues<ImageEncodeFormat>())
        {
            var ext = ImageEncodeFormatResolver.ExtensionFor(format);
            ImageEncodeFormatResolver.FromExtension(ext).Should().Be(format);
        }
    }
}
