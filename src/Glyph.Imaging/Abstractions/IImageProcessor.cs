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
    /// Writes descriptive IPTC fields (title, caption, keywords, copyright). Empty/null clears the tag.
    /// </summary>
    Task SetDescriptiveMetadataAsync(
        IImageDocument document,
        ImageDescriptiveMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns an ICC profile without converting pixel values (tags the image).
    /// </summary>
    Task AssignColorProfileAsync(
        IImageDocument document,
        ImageColorProfileKind profile,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts pixel values into the destination ICC profile (and embeds that profile).
    /// </summary>
    Task ConvertColorProfileAsync(
        IImageDocument document,
        ImageColorProfileKind profile,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies EXIF orientation into pixel data and resets the orientation tag.
    /// </summary>
    Task NormalizeOrientationAsync(IImageDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears a region to transparent (or opaque white when <paramref name="transparent"/> is false).
    /// <paramref name="kind"/> selects rectangle, ellipse, or freeform polygon within <paramref name="pixels"/> bounds.
    /// For freeform, pass document-space <paramref name="polygon"/> vertices (at least 3).
    /// When <paramref name="inverted"/> is true, clears everything outside the region (keeps the selection).
    /// </summary>
    Task ClearRectAsync(
        IImageDocument document,
        ImageRect pixels,
        bool transparent = true,
        ImageSelectionKind kind = ImageSelectionKind.Rectangle,
        IReadOnlyList<ImageMarkupPoint>? polygon = null,
        bool inverted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns BGRA32 pixels for a region (document pixel space). Ellipse/freeform clear outside the mask to transparent.
    /// When <paramref name="inverted"/> is true, returns the full image with the selection punched to transparent.
    /// </summary>
    Task<ImagePixelBuffer> ExtractRectAsync(
        IImageDocument document,
        ImageRect pixels,
        ImageSelectionKind kind = ImageSelectionKind.Rectangle,
        IReadOnlyList<ImageMarkupPoint>? polygon = null,
        bool inverted = false,
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
    /// Inverted selections are not supported.
    /// </summary>
    Task MoveRectAsync(
        IImageDocument document,
        ImageRect source,
        int destinationX,
        int destinationY,
        ImageSelectionKind kind = ImageSelectionKind.Rectangle,
        IReadOnlyList<ImageMarkupPoint>? polygon = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bakes freehand strokes and shape markup into pixel data (document pixel space).
    /// </summary>
    Task FlattenMarkupAsync(
        IImageDocument document,
        ImageMarkupLayer layer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an image file and composites it onto <paramref name="document"/> at the destination top-left.
    /// </summary>
    Task PasteFileAsync(
        IImageDocument document,
        string sourcePath,
        int destinationX,
        int destinationY,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Flood-fills from image corners with transparency using <paramref name="fuzzPercent"/>
    /// color tolerance (0–100). Useful for flat studio / solid backgrounds (F29).
    /// </summary>
    Task RemoveBackgroundAsync(
        IImageDocument document,
        double fuzzPercent = 12,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crops to the bounding box of non-transparent pixels. No-ops when the image is fully opaque
    /// or fully transparent.
    /// </summary>
    Task TrimTransparentAsync(IImageDocument document, CancellationToken cancellationToken = default);
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
    Freeform = 2,
    /// <summary>Freeform polygon with edge-snapped vertices (UI); processor treats like Freeform.</summary>
    Smart = 3,
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

/// <summary>
/// Editable descriptive metadata written primarily as IPTC (title/caption/keywords/copyright).
/// </summary>
public sealed record ImageDescriptiveMetadata(
    string? Title = null,
    string? Description = null,
    string? Keywords = null,
    string? Copyright = null);

/// <summary>Built-in ICC profiles available for assign/convert.</summary>
public enum ImageColorProfileKind
{
    Srgb = 0,
    AdobeRgb = 1,
}
