namespace Glyph.Imaging.Abstractions;

public interface IImageProcessor
{
    Task CropAsync(IImageDocument document, ImageRect pixels, CancellationToken cancellationToken = default);

    Task ResizeAsync(IImageDocument document, int width, int height, CancellationToken cancellationToken = default);

    Task RotateAsync(IImageDocument document, int degreesClockwise, CancellationToken cancellationToken = default);

    Task FlipHorizontalAsync(IImageDocument document, CancellationToken cancellationToken = default);

    Task FlipVerticalAsync(IImageDocument document, CancellationToken cancellationToken = default);

    Task AdjustAsync(IImageDocument document, ImageAdjustments adjustments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes GPS EXIF tags when present. Other metadata is left intact.
    /// </summary>
    Task RemoveGpsMetadataAsync(IImageDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies EXIF orientation into pixel data and resets the orientation tag.
    /// </summary>
    Task NormalizeOrientationAsync(IImageDocument document, CancellationToken cancellationToken = default);
}

/// <summary>
/// Axis-aligned crop rectangle in image pixel space (origin top-left).
/// </summary>
public readonly record struct ImageRect(int X, int Y, int Width, int Height);

public sealed record ImageAdjustments(
    double Brightness = 0,
    double Contrast = 0,
    double Saturation = 0);
