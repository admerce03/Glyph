using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.Editing;

namespace Glyph.Core.Tests;

public class UndoStackTests
{
    [Fact]
    public void Execute_undo_redo_round_trips_display_name_change()
    {
        var session = new DocumentSession(DocumentKind.Pdf, "original.pdf");
        session.Execute(new RenameCommand("renamed.pdf"));

        session.DisplayName.Should().Be("renamed.pdf");
        session.IsDirty.Should().BeTrue();
        session.CanUndo.Should().BeTrue();

        session.Undo();
        session.DisplayName.Should().Be("original.pdf");
        session.CanRedo.Should().BeTrue();

        session.Redo();
        session.DisplayName.Should().Be("renamed.pdf");
    }

    private sealed class RenameCommand(string newName) : IEditCommand
    {
        private string? _previous;

        public string Name => "Rename";

        public void Execute(DocumentSession session)
        {
            _previous = session.DisplayName;
            session.DisplayName = newName;
        }

        public void Undo(DocumentSession session)
        {
            session.DisplayName = _previous ?? session.DisplayName;
        }
    }
}
