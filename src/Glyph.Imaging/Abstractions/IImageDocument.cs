namespace Glyph.Imaging.Abstractions;

public interface IImageDocument : IAsyncDisposable, IDisposable
{
    string? Path { get; set; }

    int PixelWidth { get; }

    int PixelHeight { get; }

    string FormatName { get; }

    /// <summary>
    /// Number of frames after coalesce (1 for still images). Animated GIF/WebP/APNG
    /// report the full frame count (F27).
    /// </summary>
    int FrameCount { get; }

    /// <summary>
    /// Zero-based index of the frame currently exposed by <see cref="GetPixelsAsync"/>
    /// and edit operations.
    /// </summary>
    int CurrentFrameIndex { get; }

    /// <summary>
    /// Netscape-style loop count from the first frame (0 = infinite). Still images
    /// report 1.
    /// </summary>
    int AnimationIterations { get; }

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

    /// <summary>
    /// Delay before advancing from <paramref name="frameIndex"/>, in milliseconds.
    /// Zero-delay GIF frames use a 100ms browser-compatible default.
    /// </summary>
    int GetFrameDelayMilliseconds(int frameIndex);

    /// <summary>
    /// Make <paramref name="frameIndex"/> the current frame for display and edits.
    /// </summary>
    Task SetCurrentFrameAsync(int frameIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns BGRA32 pixels for a specific frame without changing
    /// <see cref="CurrentFrameIndex"/>.
    /// </summary>
    Task<ImagePixelBuffer> ExtractFrameAsync(int frameIndex, CancellationToken cancellationToken = default);
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
