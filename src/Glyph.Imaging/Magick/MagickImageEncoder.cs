using Glyph.Imaging.Abstractions;
using ImageMagick;

namespace Glyph.Imaging.Magick;

public sealed class MagickImageEncoder : IImageEncoder
{
    public Task SaveAsync(IImageDocument document, string path, CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                magick.Native.Write(path);
                magick.Path = path;
            },
            cancellationToken);
    }

    public Task SaveAsAsync(
        IImageDocument document,
        string path,
        ImageEncodeFormat format,
        ImageEncodeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var clone = magick.Native.Clone();
                clone.Format = format switch
                {
                    ImageEncodeFormat.Png => MagickFormat.Png,
                    ImageEncodeFormat.Jpeg => MagickFormat.Jpeg,
                    ImageEncodeFormat.Webp => MagickFormat.WebP,
                    ImageEncodeFormat.Bmp => MagickFormat.Bmp,
                    ImageEncodeFormat.Tiff => MagickFormat.Tiff,
                    ImageEncodeFormat.Gif => MagickFormat.Gif,
                    ImageEncodeFormat.Avif => MagickFormat.Avif,
                    ImageEncodeFormat.Jpeg2000 => MagickFormat.Jp2,
                    _ => MagickFormat.Png,
                };

                ApplyOptions(clone, format, options);
                clone.Write(path);
                magick.Path = path;
            },
            cancellationToken);
    }

    public Task WriteBgraAsync(
        ReadOnlyMemory<byte> bgra,
        int width,
        int height,
        string path,
        ImageEncodeFormat format,
        ImageEncodeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (bgra.Length < width * height * 4)
        {
            throw new ArgumentException(
                $"BGRA buffer length {bgra.Length} is shorter than {width * height * 4} bytes.",
                nameof(bgra));
        }

        var copy = bgra.ToArray();
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var image = new MagickImage();
                image.ReadPixels(
                    copy,
                    new PixelReadSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.BGRA));
                image.Format = format switch
                {
                    ImageEncodeFormat.Png => MagickFormat.Png,
                    ImageEncodeFormat.Jpeg => MagickFormat.Jpeg,
                    ImageEncodeFormat.Webp => MagickFormat.WebP,
                    ImageEncodeFormat.Bmp => MagickFormat.Bmp,
                    ImageEncodeFormat.Tiff => MagickFormat.Tiff,
                    ImageEncodeFormat.Gif => MagickFormat.Gif,
                    ImageEncodeFormat.Avif => MagickFormat.Avif,
                    ImageEncodeFormat.Jpeg2000 => MagickFormat.Jp2,
                    _ => MagickFormat.Png,
                };

                if (format is ImageEncodeFormat.Jpeg or ImageEncodeFormat.Jpeg2000)
                {
                    image.Alpha(AlphaOption.Remove);
                }

                ApplyOptions(image, format, options);
                image.Write(path);
            },
            cancellationToken);
    }

    internal static void ApplyOptions(IMagickImage<ushort> image, ImageEncodeFormat format, ImageEncodeOptions? options)
    {
        if (options is null)
        {
            return;
        }

        if (options.Quality is int quality)
        {
            if (quality is < 1 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Quality must be between 1 and 100.");
            }

            if (format is ImageEncodeFormat.Jpeg or ImageEncodeFormat.Webp or ImageEncodeFormat.Avif)
            {
                image.Quality = (uint)quality;
            }
        }

        if (format == ImageEncodeFormat.Webp && options.Lossless == true)
        {
            image.Settings.SetDefine(MagickFormat.WebP, "lossless", true);
        }
    }

    private static MagickImageDocument RequireMagick(IImageDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document is not MagickImageDocument magick)
        {
            throw new ArgumentException("Document must be opened by MagickImageDecoder.", nameof(document));
        }

        return magick;
    }
}
