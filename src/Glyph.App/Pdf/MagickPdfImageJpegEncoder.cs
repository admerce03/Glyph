using Glyph.Pdf.Abstractions;
using ImageMagick;

namespace Glyph.App.Pdf;

/// <summary>
/// Magick.NET JPEG encoder for PDF optimize (keeps Magick out of Glyph.Pdf).
/// </summary>
internal sealed class MagickPdfImageJpegEncoder : IPdfImageJpegEncoder
{
    public byte[]? EncodeBgraToJpeg(ReadOnlySpan<byte> bgra, int width, int height, int quality)
    {
        if (width <= 0 || height <= 0 || bgra.Length < width * height * 4)
        {
            return null;
        }

        quality = Math.Clamp(quality, 1, 100);
        var copy = bgra.ToArray();
        using var image = new MagickImage();
        image.ReadPixels(copy, new PixelReadSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.BGRA));
        image.Format = MagickFormat.Jpeg;
        image.Quality = (uint)quality;
        image.Alpha(AlphaOption.Remove);
        return image.ToByteArray();
    }
}
