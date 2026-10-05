using System.Collections.Concurrent;

namespace Glyph.Pdf.Text;

/// <summary>
/// In-memory per-page text layer extracted for background Find indexing (F57-05 / F58-06).
/// </summary>
public sealed class PdfPageTextIndex
{
    private readonly ConcurrentDictionary<int, string> _pages = new();
    private int _pageCount = -1;
    private int _complete; // 0/1

    public int PageCount => Volatile.Read(ref _pageCount);

    public bool IsComplete => Volatile.Read(ref _complete) == 1;

    public int IndexedPageCount => _pages.Count;

    public void SetPageCount(int pageCount) =>
        Interlocked.Exchange(ref _pageCount, pageCount);

    public void SetPage(int pageIndex, string text) =>
        _pages[pageIndex] = text ?? string.Empty;

    public bool TryGetPage(int pageIndex, out string text) =>
        _pages.TryGetValue(pageIndex, out text!);

    public void MarkComplete() => Interlocked.Exchange(ref _complete, 1);

    public IReadOnlyList<(int PageIndex, string Text)> Snapshot() =>
        _pages.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)).ToArray();
}
