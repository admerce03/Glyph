namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Engine-agnostic PDF document handle. Implementations must support large documents
/// without rasterizing all pages up front.
/// </summary>
public interface IPdfDocument : IAsyncDisposable, IDisposable
{
    string? Path { get; set; }

    int PageCount { get; }

    bool IsEncrypted { get; }

    /// <summary>
    /// Raised after page-tree mutations (rotate/delete/reorder/insert) so viewers can refresh.
    /// </summary>
    event EventHandler? PagesChanged;

    IPdfPage GetPage(int pageIndex);
}
