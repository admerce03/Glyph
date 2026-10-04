using Glyph.Core.Documents;

namespace Glyph.Core.Editing;

public sealed class UndoStack
{
    private readonly Stack<IEditCommand> _undo = new();
    private readonly Stack<IEditCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Push(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _undo.Push(command);
        _redo.Clear();
    }

    public bool TryUndo(DocumentSession session, out IEditCommand? command)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (_undo.Count == 0)
        {
            command = null;
            return false;
        }

        command = _undo.Pop();
        command.Undo(session);
        _redo.Push(command);
        return true;
    }

    public bool TryRedo(DocumentSession session, out IEditCommand? command)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (_redo.Count == 0)
        {
            command = null;
            return false;
        }

        command = _redo.Pop();
        command.Execute(session);
        _undo.Push(command);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
