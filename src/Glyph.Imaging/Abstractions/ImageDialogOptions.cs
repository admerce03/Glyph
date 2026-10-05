namespace Glyph.Imaging.Abstractions;

/// <summary>
/// ComboBox / list option labels for image viewer dialogs and chrome.
/// </summary>
public static class ImageDialogOptions
{
    public static IReadOnlyList<string> CropAspectRatios { get; } =
    [
        "Free",
        "Original",
        "1:1",
        "4:3",
        "3:2",
        "16:9",
    ];

    public static IReadOnlyList<string> SelectionTools { get; } =
    [
        "Rect",
        "Ellipse",
        "Lasso",
        "Smart",
    ];

    public static IReadOnlyList<string> SizeUnits { get; } =
    [
        "Pixels",
        "Inches",
        "Centimeters",
    ];

    public static IReadOnlyList<string> ResamplingModes { get; } =
    [
        "Auto",
        "Nearest-neighbor",
        "Bilinear",
        "Bicubic",
    ];

    public static IReadOnlyList<string> BatchCategories { get; } =
    [
        "Orientation",
        "Convert / export",
        "Strip metadata",
        "Rename",
        "Color profile",
    ];

    public static IReadOnlyList<string> BatchOrientations { get; } =
    [
        "Rotate left 90°",
        "Rotate right 90°",
        "Rotate 180°",
        "Flip horizontal",
        "Flip vertical",
        "Normalize EXIF orientation",
    ];

    public static IReadOnlyList<string> ExportFormats { get; } =
    [
        "PNG",
        "JPEG",
        "WebP",
        "TIFF",
        "BMP",
        "GIF",
        "AVIF",
        "JPEG 2000",
    ];

    public static IReadOnlyList<string> ColorProfileOps { get; } =
    [
        "Assign sRGB",
        "Convert → sRGB",
        "Assign Adobe RGB",
        "Convert → Adobe RGB",
    ];

    public static IReadOnlyList<string> PrintScales { get; } =
    [
        "Fit to printable area",
        "Fill page",
        "Actual size",
    ];

    public static IReadOnlyList<string> PagesPerSheet { get; } =
    [
        "1",
        "2",
        "4",
    ];

    public static IReadOnlyList<string> ConvertFormatsExtra { get; } =
    [
        "WebP",
        "TIFF",
        "BMP",
        "GIF",
        "AVIF",
        "JPEG 2000",
        "HEIC",
        "PDF",
    ];

    public static IReadOnlyList<string> TiffCompressions { get; } =
    [
        "Default",
        "None",
        "LZW",
        "ZIP",
        "JPEG",
    ];

    public static IReadOnlyList<string> MarkupTools { get; } =
    [
        "Freehand",
        "Rectangle",
        "Ellipse",
        "Line",
        "Arrow",
        "Text",
        "Callout",
    ];

    public static IReadOnlyList<string> MarkupColors { get; } =
    [
        "Red",
        "Black",
        "White",
        "Yellow",
        "Blue",
        "Green",
    ];
}
