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
                // Bake EXIF orientation into pixels so dimensions match what users see.
                image.AutoOrient();
                ClearExifOrientation(image);
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

    internal static void ClearExifOrientation(IMagickImage<ushort> image)
    {
        image.Orientation = OrientationType.TopLeft;
        var exif = image.GetExifProfile();
        if (exif is null)
        {
            return;
        }

        exif.SetValue(ExifTag.Orientation, (ushort)1);
        image.SetProfile(exif);
    }
}
