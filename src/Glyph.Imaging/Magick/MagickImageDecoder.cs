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
                using var collection = new MagickImageCollection(path);
                if (collection.Count <= 1)
                {
                    var single = collection.Count == 1
                        ? (MagickImage)collection[0].Clone()
                        : new MagickImage(path);
                    single.AutoOrient();
                    ClearExifOrientation(single);
                    return (IImageDocument)new MagickImageDocument(path, single);
                }

                // Coalesce so each frame is a full composited image (GIF offsets/disposal).
                collection.Coalesce();
                var frames = new List<MagickImage>(collection.Count);
                foreach (var frame in collection)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var clone = (MagickImage)frame.Clone();
                    clone.AutoOrient();
                    ClearExifOrientation(clone);
                    frames.Add(clone);
                }

                return new MagickImageDocument(path, frames);
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
