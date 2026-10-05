namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Display rendering-intent combo labels (F39-09).
/// </summary>
public static class ImageRenderingIntentUi
{
    public static IReadOnlyList<string> Labels { get; } =
    [
        "Perceptual",
        "Relative",
        "Saturation",
        "Absolute",
    ];

    public static string StatusAfterChange(ImageRenderingIntent intent) =>
        "Rendering intent: " + Labels[(int)intent];

    public static ImageRenderingIntent FromComboIndex(int selectedIndex) =>
        (ImageRenderingIntent)Math.Clamp(selectedIndex, 0, Labels.Count - 1);
}
