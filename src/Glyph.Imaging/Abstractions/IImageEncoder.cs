namespace Glyph.Imaging.Abstractions;

public interface IImageEncoder
{
    Task SaveAsync(IImageDocument document, string path, CancellationToken cancellationToken = default);

    Task SaveAsAsync(
        IImageDocument document,
        string path,
        ImageEncodeFormat format,
        ImageEncodeOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encode tightly packed BGRA32 pixels (e.g. PDF page renders) to an image file.
    /// </summary>
    Task WriteBgraAsync(
        ReadOnlyMemory<byte> bgra,
        int width,
        int height,
        string path,
        ImageEncodeFormat format,
        ImageEncodeOptions? options = null,
        CancellationToken cancellationToken = default);
}

public enum ImageEncodeFormat
{
    Png,
    Jpeg,
    Webp,
    Bmp,
    Tiff,
    Gif,
    Avif,
    Jpeg2000,
    Heic,
}

/// <summary>
/// Optional encode knobs. Quality is 1–100 when set (JPEG/WebP/AVIF/HEIC).
/// Lossless applies to WebP when true.
/// Title/Author are written into image metadata when the codec supports it.
/// EmbedSrgbProfile writes an sRGB ICC profile when the codec retains it
/// (PNG needs <c>png:preserve-iCCP</c>; JPEG 2000 may drop the profile).
/// </summary>
public sealed record ImageEncodeOptions(
    int? Quality = null,
    bool? Lossless = null,
    string? Title = null,
    string? Author = null,
    bool EmbedSrgbProfile = false,
    /// <summary>When false, strip alpha before writing (JPEG always strips).</summary>
    bool PreserveAlpha = true,
    /// <summary>When false, strip EXIF/IPTC/XMP and other profiles before writing.</summary>
    bool PreserveMetadata = true);
