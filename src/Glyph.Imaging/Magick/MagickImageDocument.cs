using Glyph.Imaging.Abstractions;
using ImageMagick;

namespace Glyph.Imaging.Magick;

public sealed class MagickImageDocument : IImageDocument
{
    private MagickImage _image;
    private bool _disposed;

    internal MagickImageDocument(string? path, MagickImage image)
    {
        Path = path;
        _image = image;
    }

    public string? Path { get; set; }

    public int PixelWidth => checked((int)_image.Width);

    public int PixelHeight => checked((int)_image.Height);

    public string FormatName => _image.Format.ToString();

    internal MagickImage Native
    {
        get
        {
            ThrowIfDisposed();
            return _image;
        }
    }

    internal void Replace(MagickImage image)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(image);
        if (!ReferenceEquals(_image, image))
        {
            _image.Dispose();
            _image = image;
        }
    }

    public Task<ImagePixelBuffer> GetPixelsAsync(int? maxEdge = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var clone = _image.Clone();
                if (maxEdge is int edge && edge > 0)
                {
                    var longest = Math.Max(clone.Width, clone.Height);
                    if (longest > (uint)edge)
                    {
                        clone.Resize(new MagickGeometry((uint)edge)
                        {
                            Greater = true,
                            IgnoreAspectRatio = false,
                        });
                    }
                }

                clone.AutoOrient();
                // Emit 8-bit BGRA32 even when Magick.NET is built as Q16.
                clone.Depth = 8;
                clone.ColorType = ColorType.TrueColorAlpha;
                var pixels = clone.ToByteArray(MagickFormat.Bgra);
                return new ImagePixelBuffer(checked((int)clone.Width), checked((int)clone.Height), pixels);
            },
            cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _image.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
