namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Convert/export embed-sRGB checkbox (F35-17).
/// </summary>
public static class ImageEncodeEmbedSrgb
{
    public const string CheckboxLabel = "Embed sRGB ICC profile";

    public static ImageEncodeOptions WithEmbedFlag(
        ImageEncodeOptions options,
        bool embedSrgbProfile) =>
        options with { EmbedSrgbProfile = embedSrgbProfile };
}
