using Glyph.Core.Editing;

namespace Glyph.Core.Documents;

/// <summary>
/// In-memory representation of an open document. Does not own decoded rasters.
/// </summary>
public sealed class DocumentSession
{
    private readonly UndoStack _undoStack = new();

    public DocumentSession(DocumentKind kind, string displayName, string? path = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Id = DocumentId.New();
        Kind = kind;
        DisplayName = displayName;
        Path = path;
        OpenedAtUtc = DateTimeOffset.UtcNow;
    }

    public DocumentId Id { get; }

    public DocumentKind Kind { get; }

    public string DisplayName { get; set; }

    public string? Path { get; set; }

    public bool IsDirty { get; private set; }

    public bool IsReadOnly { get; set; }

    public DateTimeOffset OpenedAtUtc { get; }

    public DocumentViewState ViewState { get; } = new();

    public bool CanUndo => _undoStack.CanUndo;

    public bool CanRedo => _undoStack.CanRedo;

    public void MarkDirty() => IsDirty = true;

    public void MarkClean() => IsDirty = false;

    public void Execute(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsReadOnly)
        {
            throw new InvalidOperationException("Cannot edit a read-only document.");
        }

        command.Execute(this);
        _undoStack.Push(command);
        IsDirty = true;
    }

    public void Undo()
    {
        if (!_undoStack.TryUndo(this, out _))
        {
            return;
        }

        IsDirty = true;
    }

    public void Redo()
    {
        if (!_undoStack.TryRedo(this, out _))
        {
            return;
        }

        IsDirty = true;
    }
}
