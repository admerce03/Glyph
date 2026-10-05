using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Info;

/// <summary>
/// LIFO stack of prior document Info dictionary edits for Ctrl+Z (F49-10).
/// </summary>
public sealed class DocumentInfoUndoStack
{
    private readonly Stack<PdfDocumentInfoUpdate> _entries = new();

    public int Count => _entries.Count;

    public void Push(PdfDocumentInfoUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        _entries.Push(update);
    }

    public bool TryPop(out PdfDocumentInfoUpdate update)
    {
        if (_entries.Count == 0)
        {
            update = null!;
            return false;
        }

        update = _entries.Pop();
        return true;
    }

    /// <summary>Discard the most recent undo snapshot (e.g. when an edit fails after push).</summary>
    public bool TryDiscardTop()
    {
        if (_entries.Count == 0)
        {
            return false;
        }

        _entries.Pop();
        return true;
    }

    public void Clear() => _entries.Clear();
}
