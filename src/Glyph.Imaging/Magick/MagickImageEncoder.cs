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
                    _ => MagickFormat.Png,
                };
                clone.Write(path);
                magick.Path = path;
            },
            cancellationToken);
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
