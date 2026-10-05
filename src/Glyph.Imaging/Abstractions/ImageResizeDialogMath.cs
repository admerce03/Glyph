namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Resize dialog helpers: filter combo, aspect lock, and raw BGRA size estimate (F31-01..04/08/12).
/// </summary>
public static class ImageResizeDialogMath
{
    public static ImageResizeFilter FilterFromComboIndex(int selectedIndex) =>
        (ImageResizeFilter)Math.Clamp(selectedIndex, 0, 3);

    public static int ComboIndexFromPreferenceName(string? name) =>
        name switch
        {
            "NearestNeighbor" => 1,
            "Bilinear" => 2,
            "Bicubic" => 3,
            _ => 0, // Auto / unknown
        };

    /// <summary>
    /// Approximate uncompressed BGRA size in mebibytes.
    /// </summary>
    public static double EstimateRawBgraMegabytes(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        return width * (double)height * 4.0 / (1024.0 * 1024.0);
    }

    public static int HeightForWidth(int width, double aspectRatio) =>
        Math.Max(1, (int)Math.Round(Math.Max(1, width) / Math.Max(aspectRatio, 1e-9)));

    public static int WidthForHeight(int height, double aspectRatio) =>
        Math.Max(1, (int)Math.Round(Math.Max(1, height) * Math.Max(aspectRatio, 0)));

    public static (int Width, int Height) ScaleByPercent(int sourceWidth, int sourceHeight, double percent)
    {
        var factor = Math.Max(0.01, percent) / 100.0;
        return (
            Math.Max(1, (int)Math.Round(sourceWidth * factor)),
            Math.Max(1, (int)Math.Round(sourceHeight * factor)));
    }

    public static string FormatResultEstimate(int width, int height) =>
        $"Result: {width}×{height} px · ~{EstimateRawBgraMegabytes(width, height):0.##} MB raw";

    public static string FormatResultWithDpi(int width, int height, double dpi) =>
        $"Result: {width}×{height} px @ {dpi:0.#} DPI · ~{EstimateRawBgraMegabytes(width, height):0.##} MB raw BGRA";

    public static string FormatCurrent(int width, int height, double dpi) =>
        $"Current: {width}×{height} px · {dpi:0.#} DPI";

    public static string FormatAlsoResizeFolder(int siblingCount) =>
        siblingCount > 0
            ? $"Also resize all {siblingCount} images in folder (scale %)"
            : "Also resize folder images";

    public static string FormatFuzzPercent(double value) =>
        $"Fuzz {value:0}%";
}
