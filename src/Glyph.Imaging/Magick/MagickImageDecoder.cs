using Glyph.Imaging.Abstractions;
using ImageMagick;

namespace Glyph.Imaging.Magick;

public sealed class MagickImageDecoder : IImageDecoder
{
    public Task<IImageDocument> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var image = new MagickImage(path);
                return (IImageDocument)new MagickImageDocument(path, image);
            },
            cancellationToken);
    }

    public Task<ImageInfo> ProbeAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new MagickImageInfo(path);
                return new ImageInfo(checked((int)info.Width), checked((int)info.Height), info.Format.ToString());
            },
            cancellationToken);
    }
}
