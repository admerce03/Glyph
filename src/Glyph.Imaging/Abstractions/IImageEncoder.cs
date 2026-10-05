namespace Glyph.Imaging.Abstractions;

public interface IImageEncoder
{
    Task SaveAsync(IImageDocument document, string path, CancellationToken cancellationToken = default);

    Task SaveAsAsync(
        IImageDocument document,
        string path,
        ImageEncodeFormat format,
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
