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
}

public enum ImageEncodeFormat
{
    Png,
    Jpeg,
    Webp,
    Bmp,
    Tiff,
    Gif,
}

/// <summary>
/// Optional encode knobs. Quality is 1–100 when set (JPEG/WebP).
/// Lossless applies to WebP when true.
/// </summary>
public sealed record ImageEncodeOptions(int? Quality = null, bool? Lossless = null);
