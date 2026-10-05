using System.Collections.Concurrent;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Redaction;

/// <summary>
/// In-memory pending redaction marks keyed by document instance.
/// </summary>
internal sealed class PdfRedactionPendingStore
{
    private readonly ConcurrentDictionary<IPdfDocument, List<PdfPendingRedaction>> _pending = new();

    public IReadOnlyList<PdfPendingRedaction> Get(IPdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return _pending.TryGetValue(document, out var list)
            ? list.ToArray()
            : [];
    }

    public PdfPendingRedaction Add(IPdfDocument document, PdfPendingRedaction mark)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(mark);
        var list = _pending.GetOrAdd(document, static _ => []);
        lock (list)
        {
            list.Add(mark);
        }

        return mark;
    }

    public bool Remove(IPdfDocument document, Guid id)
    {
        if (!_pending.TryGetValue(document, out var list))
        {
            return false;
        }

        lock (list)
        {
            var index = list.FindIndex(m => m.Id == id);
            if (index < 0)
            {
                return false;
            }

            list.RemoveAt(index);
            return true;
        }
    }

    public void Clear(IPdfDocument document)
    {
        _pending.TryRemove(document, out _);
    }
}
