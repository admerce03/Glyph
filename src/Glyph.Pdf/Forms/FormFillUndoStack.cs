using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Forms;

/// <summary>
/// LIFO stack of prior AcroForm field values for Ctrl+Z form-fill undo (F49-11).
/// </summary>
public sealed class FormFillUndoStack
{
    private readonly Stack<FormFillUndoEntry> _entries = new();

    public int Count => _entries.Count;

    public void Push(FormFillUndoEntry entry) => _entries.Push(entry);

    public void Push(PdfFormFieldInfo field)
    {
        ArgumentNullException.ThrowIfNull(field);
        Push(new FormFillUndoEntry(
            field.PageIndex,
            field.AnnotIndex,
            field.Kind,
            field.Value ?? string.Empty));
    }

    public bool TryPop(out FormFillUndoEntry entry)
    {
        if (_entries.Count == 0)
        {
            entry = default!;
            return false;
        }

        entry = _entries.Pop();
        return true;
    }

    public void Clear() => _entries.Clear();
}

public sealed record FormFillUndoEntry(
    int PageIndex,
    int AnnotIndex,
    PdfFormFieldKind Kind,
    string PreviousValue);
