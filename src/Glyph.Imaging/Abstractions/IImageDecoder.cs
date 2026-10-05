namespace Glyph.Imaging.Abstractions;

public interface IImageDecoder
{
    Task<IImageDocument> OpenAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads width/height/format without necessarily decoding pixel data.
    /// </summary>
    Task<ImageInfo> ProbeAsync(string path, CancellationToken cancellationToken = default);
}

public sealed record ImageInfo(int PixelWidth, int PixelHeight, string FormatName);
