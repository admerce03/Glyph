using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Rendering;

/// <summary>
/// Bounded LRU cache of rendered page bitmaps keyed by document/page/scale.
/// </summary>
public sealed class PageRenderCache
{
    private readonly int _capacity;
    private readonly Dictionary<string, LinkedListNode<Entry>> _map = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> _lru = new();
    private readonly object _gate = new();

    public PageRenderCache(int capacity = 32)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _map.Count;
            }
        }
    }

    public bool TryGet(string documentKey, int pageIndex, double scale, out PdfRenderResult? result)
    {
        var key = MakeKey(documentKey, pageIndex, scale);
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
                result = node.Value.Result;
                return true;
            }
        }

        result = null;
        return false;
    }

    public void Set(string documentKey, int pageIndex, double scale, PdfRenderResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var key = MakeKey(documentKey, pageIndex, scale);

        lock (_gate)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                existing.Value.Result.Dispose();
                _lru.Remove(existing);
                _map.Remove(key);
            }

            var entry = new Entry(key, result);
            var node = _lru.AddFirst(entry);
            _map[key] = node;

            while (_map.Count > _capacity)
            {
                var last = _lru.Last!;
                _lru.RemoveLast();
                _map.Remove(last.Value.Key);
                last.Value.Result.Dispose();
            }
        }
    }

    public void ClearDocument(string documentKey)
    {
        lock (_gate)
        {
            var prefix = documentKey + "|";
            var doomed = _map.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            foreach (var key in doomed)
            {
                var node = _map[key];
                _lru.Remove(node);
                _map.Remove(key);
                node.Value.Result.Dispose();
            }
        }
    }

    private static string MakeKey(string documentKey, int pageIndex, double scale) =>
        $"{documentKey}|{pageIndex}|{scale:0.####}";

    private sealed record Entry(string Key, PdfRenderResult Result);
}
