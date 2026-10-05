namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Batch color-profile assign/convert labels (F36-07).
/// </summary>
public static class ImageBatchColorProfilePolicy
{
    public const string BatchTitle = "Batch color profile";

    public static string UpdatedStatus(int count) =>
        $"Batch color profile: updated {count} folder image(s)";

    public static string CancelledStatus(int updated) =>
        BatchProgressUi.CancelledStatus("Batch color profile", updated);

    public static ImageColorProfileKind FromComboIndex(int selectedIndex) =>
        selectedIndex == 1 ? ImageColorProfileKind.AdobeRgb : ImageColorProfileKind.Srgb;
}
