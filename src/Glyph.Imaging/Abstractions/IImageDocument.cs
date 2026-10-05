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

    /// <summary>
    /// Reads dimensions, density, color info, and common EXIF/GPS fields.
    /// </summary>
    Task<ImageMetadataInfo> GetMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Snapshot current pixels/metadata for undo (F30-07). Caller must dispose
    /// unused checkpoints.
    /// </summary>
    IImageEditCheckpoint CaptureCheckpoint();

    /// <summary>
    /// Restore a checkpoint captured from this document. Disposes the checkpoint.
    /// </summary>
    void RestoreCheckpoint(IImageEditCheckpoint checkpoint);
}

/// <summary>
/// Opaque undo snapshot for <see cref="IImageDocument"/>.
/// </summary>
public interface IImageEditCheckpoint : IDisposable
{
    /// <summary>Deep-copies the checkpoint so a restore can consume the clone while the original remains.</summary>
    IImageEditCheckpoint Clone();
}

public sealed record ImagePixelBuffer(int Width, int Height, byte[] BgraPixels);
