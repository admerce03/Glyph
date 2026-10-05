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
                    ImageEncodeFormat.Heic => MagickFormat.Heic,
                    _ => MagickFormat.Png,
                };

                ApplyOptions(clone, format, options);
                if (options?.PreserveMetadata == false)
                {
                    clone.Strip();
                    // Re-apply encode knobs that Strip may have cleared (title/author/ICC).
                    ApplyOptions(clone, format, options);
                }

                if (options?.PreserveAlpha == false
                    || format is ImageEncodeFormat.Jpeg or ImageEncodeFormat.Jpeg2000)
                {
                    clone.Alpha(AlphaOption.Remove);
                }

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
                    ImageEncodeFormat.Heic => MagickFormat.Heic,
                    _ => MagickFormat.Png,
                };

                if (options?.PreserveMetadata == false)
                {
                    image.Strip();
                }

                ApplyOptions(image, format, options);

                if (options?.PreserveAlpha == false
                    || format is ImageEncodeFormat.Jpeg or ImageEncodeFormat.Jpeg2000)
                {
                    image.Alpha(AlphaOption.Remove);
                }

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

            if (format is ImageEncodeFormat.Jpeg or ImageEncodeFormat.Webp or ImageEncodeFormat.Avif
                or ImageEncodeFormat.Heic)
            {
                image.Quality = (uint)quality;
            }
        }

        if (format == ImageEncodeFormat.Webp && options.Lossless == true)
        {
            image.Settings.SetDefine(MagickFormat.WebP, "lossless", true);
        }

        if (options.EmbedSrgbProfile)
        {
            // ImageMagick drops the standard sRGB ICC from PNG unless preserve-iCCP is set.
            if (format == ImageEncodeFormat.Png)
            {
                image.Settings.SetDefine("png:preserve-iCCP", "true");
            }

            image.SetProfile(ColorProfiles.SRGB);
        }

        if (!string.IsNullOrWhiteSpace(options.Title))
        {
            image.SetAttribute("Title", options.Title);
            try
            {
                image.SetAttribute("exif:ImageDescription", options.Title);
            }
            catch
            {
                // Some codecs reject EXIF keys; Title attribute is enough.
            }
        }

        if (!string.IsNullOrWhiteSpace(options.Author))
        {
            image.SetAttribute("Artist", options.Author);
            try
            {
                image.SetAttribute("exif:Artist", options.Author);
            }
            catch
            {
            }
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
