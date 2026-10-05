using Glyph.Imaging.Abstractions;
using ImageMagick;

namespace Glyph.Imaging.Magick;

internal sealed class MagickImageEditCheckpoint : IImageEditCheckpoint
{
    private MagickImage? _image;

    public MagickImageEditCheckpoint(MagickImage image)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
    }

    public MagickImage TakeOwnership()
    {
        var image = _image ?? throw new ObjectDisposedException(nameof(MagickImageEditCheckpoint));
        _image = null;
        return image;
    }

    public IImageEditCheckpoint Clone()
    {
        var image = _image ?? throw new ObjectDisposedException(nameof(MagickImageEditCheckpoint));
        return new MagickImageEditCheckpoint((MagickImage)image.Clone());
    }

    public void Dispose()
    {
        _image?.Dispose();
        _image = null;
    }
}
