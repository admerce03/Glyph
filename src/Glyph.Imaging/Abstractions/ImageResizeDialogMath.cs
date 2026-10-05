namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Resize dialog helpers: filter combo mapping and raw BGRA size estimate (F31-08/12).
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
}
