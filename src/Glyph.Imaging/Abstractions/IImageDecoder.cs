namespace Glyph.Imaging.Abstractions;

public interface IImageDecoder
{
    Task<IImageDocument> OpenAsync(string path, CancellationToken cancellationToken = default);
}
