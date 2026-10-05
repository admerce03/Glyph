namespace Glyph.Imaging.Abstractions;

public interface IImageProcessor
{
    Task CropAsync(IImageDocument document, ImageRect pixels, CancellationToken cancellationToken = default);

    Task ResizeAsync(
        IImageDocument document,
        int width,
        int height,
        ImageResizeOptions? options = null,
        CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Clears a region to transparent (or opaque white when <paramref name="transparent"/> is false).
    /// <paramref name="kind"/> selects rectangle or ellipse geometry within <paramref name="pixels"/> bounds.
    /// </summary>
    Task ClearRectAsync(
        IImageDocument document,
        ImageRect pixels,
        bool transparent = true,
        ImageSelectionKind kind = ImageSelectionKind.Rectangle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns BGRA32 pixels for a region (document pixel space). Ellipse kind clears outside the oval to transparent.
    /// </summary>
    Task<ImagePixelBuffer> ExtractRectAsync(
        IImageDocument document,
        ImageRect pixels,
        ImageSelectionKind kind = ImageSelectionKind.Rectangle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Composites a BGRA32 buffer onto the document at <paramref name="destinationX"/>,<paramref name="destinationY"/> (top-left).
    /// </summary>
    Task PasteRectAsync(
        IImageDocument document,
        ImagePixelBuffer source,
        int destinationX,
        int destinationY,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves pixels from <paramref name="source"/> to a new top-left
    /// (<paramref name="destinationX"/>, <paramref name="destinationY"/>), clearing the source
    /// region to transparent. No-ops when the destination equals the source origin.
    /// </summary>
    Task MoveRectAsync(
        IImageDocument document,
        ImageRect source,
        int destinationX,
        int destinationY,
        ImageSelectionKind kind = ImageSelectionKind.Rectangle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bakes freehand strokes and shape markup into pixel data (document pixel space).
    /// </summary>
    Task FlattenMarkupAsync(
        IImageDocument document,
        ImageMarkupLayer layer,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Axis-aligned crop rectangle in image pixel space (origin top-left).
/// </summary>
public readonly record struct ImageRect(int X, int Y, int Width, int Height);

/// <summary>
/// Geometry used for pixel selection operations (clear/extract/move).
/// </summary>
public enum ImageSelectionKind
{
    Rectangle = 0,
    Ellipse = 1,
}

public enum ImageResizeFilter
{
    Auto = 0,
    NearestNeighbor = 1,
    Bilinear = 2,
    Bicubic = 3,
}

/// <summary>
/// Optional resize knobs: resampling filter and output density (DPI).
/// </summary>
public sealed record ImageResizeOptions(
    ImageResizeFilter Filter = ImageResizeFilter.Auto,
    double? DensityDpi = null);

public sealed record ImageAdjustments(
    double Brightness = 0,
    double Contrast = 0,
    double Saturation = 0,
    bool AutoLevels = false,
    double Sharpness = 0,
    bool Sepia = false,
    double Temperature = 0,
    double Tint = 0,
    double Highlights = 0,
    double Shadows = 0,
    /// <summary>Black-point percentage for levels (0–100; higher crushes more shadows).</summary>
    double BlackPoint = 0,
    /// <summary>White-point percentage for levels (0–100; lower clips more highlights).</summary>
    double WhitePoint = 100,
    /// <summary>Gamma multiplier (typically 0.1–3.0; 1.0 = unchanged).</summary>
    double Gamma = 1.0);
