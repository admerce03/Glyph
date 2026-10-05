namespace Glyph.Imaging.Abstractions;

public interface IImageDocument : IAsyncDisposable, IDisposable
{
    string? Path { get; set; }

    int PixelWidth { get; }

    int PixelHeight { get; }

    string FormatName { get; }

    /// <summary>
    /// Returns BGRA32 pixels for the full image (or a downscaled preview when
    /// <paramref name="maxEdge"/> is set). May decode lazily on first call.
    /// </summary>
    Task<ImagePixelBuffer> GetPixelsAsync(int? maxEdge = null, CancellationToken cancellationToken = default);
}

public sealed record ImagePixelBuffer(int Width, int Height, byte[] BgraPixels);
