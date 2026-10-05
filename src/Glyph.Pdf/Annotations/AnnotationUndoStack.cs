using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Annotations;

/// <summary>
/// LIFO stack of recently created annotations for stroke/signature/markup undo (F18-06 / F49-01/12).
/// </summary>
public sealed class AnnotationUndoStack
{
    private readonly Stack<PdfAnnotationInfo> _entries = new();

    public int Count => _entries.Count;

    public void Push(PdfAnnotationInfo annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        _entries.Push(annotation);
    }

    public bool TryPeek(out PdfAnnotationInfo annotation)
    {
        if (_entries.Count == 0)
        {
            annotation = null!;
            return false;
        }

        annotation = _entries.Peek();
        return true;
    }

    public bool TryPop(out PdfAnnotationInfo annotation)
    {
        if (_entries.Count == 0)
        {
            annotation = null!;
            return false;
        }

        annotation = _entries.Pop();
        return true;
    }

    /// <summary>
    /// Pops the top entry only when it matches the given page/annot indexes
    /// (used when replacing a freehand stroke with a cleaned shape).
    /// </summary>
    public bool TryPopIfMatches(int pageIndex, int annotIndex)
    {
        if (_entries.Count == 0)
        {
            return false;
        }

        var top = _entries.Peek();
        if (top.PageIndex != pageIndex || top.AnnotIndex != annotIndex)
        {
            return false;
        }

        _entries.Pop();
        return true;
    }

    public void Clear() => _entries.Clear();
}
