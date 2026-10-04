namespace Glyph.Imaging.Abstractions;

public interface IImageDocument : IAsyncDisposable, IDisposable
{
    string? Path { get; }

    int PixelWidth { get; }

    int PixelHeight { get; }

    string FormatName { get; }
}
