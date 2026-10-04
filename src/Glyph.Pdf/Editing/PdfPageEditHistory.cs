using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Editing;

/// <summary>
/// Snapshot-based undo/redo for page-tree mutations on a single open PDF.
/// </summary>
public sealed class PdfPageEditHistory
{
    private readonly Stack<byte[]> _undo = new();
    private readonly Stack<byte[]> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public async Task ExecuteAsync(
        IPdfDocument document,
        IPdfPageEditor editor,
        Func<Task> mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(mutation);

        var before = await editor.SaveToBytesAsync(document, cancellationToken).ConfigureAwait(false);
        await mutation().ConfigureAwait(false);
        _undo.Push(before);
        _redo.Clear();
    }

    public async Task UndoAsync(
        IPdfDocument document,
        IPdfPageEditor editor,
        CancellationToken cancellationToken = default)
    {
        if (_undo.Count == 0)
        {
            return;
        }

        var before = _undo.Pop();
        var current = await editor.SaveToBytesAsync(document, cancellationToken).ConfigureAwait(false);
        await editor.RestoreAsync(document, before, cancellationToken).ConfigureAwait(false);
        _redo.Push(current);
    }

    public async Task RedoAsync(
        IPdfDocument document,
        IPdfPageEditor editor,
        CancellationToken cancellationToken = default)
    {
        if (_redo.Count == 0)
        {
            return;
        }

        var next = _redo.Pop();
        var current = await editor.SaveToBytesAsync(document, cancellationToken).ConfigureAwait(false);
        await editor.RestoreAsync(document, next, cancellationToken).ConfigureAwait(false);
        _undo.Push(current);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
