namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Pixel buffer ownership transfers to the caller.
/// Format is BGRA32, top-down.
/// </summary>
public sealed class PdfRenderResult : IDisposable
{
    private byte[]? _pixels;

    public PdfRenderResult(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(pixels);

        Width = width;
        Height = height;
        _pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public ReadOnlySpan<byte> Pixels => _pixels ?? throw new ObjectDisposedException(nameof(PdfRenderResult));

    public void Dispose()
    {
        _pixels = null;
    }
}
