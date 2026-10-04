using Glyph.Core.Documents;

namespace Glyph.Core.Editing;

/// <summary>
/// A document-local undoable mutation.
/// </summary>
public interface IEditCommand
{
    string Name { get; }

    void Execute(DocumentSession session);

    void Undo(DocumentSession session);
}
