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
}

/// <summary>
/// Optional encode knobs. Quality is 1–100 when set (JPEG/WebP/AVIF).
/// Lossless applies to WebP when true.
/// Title/Author are written into image metadata when the codec supports it.
/// </summary>
public sealed record ImageEncodeOptions(
    int? Quality = null,
    bool? Lossless = null,
    string? Title = null,
    string? Author = null);
