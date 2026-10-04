namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Engine-agnostic PDF document handle. Implementations must support large documents
/// without rasterizing all pages up front.
/// </summary>
public interface IPdfDocument : IAsyncDisposable, IDisposable
{
    string? Path { get; }

    int PageCount { get; }

    bool IsEncrypted { get; }

    IPdfPage GetPage(int pageIndex);
}
