using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.Editing;

namespace Glyph.Core.Tests;

public class DocumentSessionTests
{
    [Fact]
    public void MarkDirty_and_MarkClean_toggle_dirty_flag()
    {
        var session = new DocumentSession(DocumentKind.Pdf, "a.pdf", @"C:\a.pdf");
        session.IsDirty.Should().BeFalse();
        session.MarkDirty();
        session.IsDirty.Should().BeTrue();
        session.MarkClean();
        session.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void Execute_on_read_only_throws_and_does_not_dirty()
    {
        var session = new DocumentSession(DocumentKind.Pdf, "locked.pdf")
        {
            IsReadOnly = true,
        };

        var act = () => session.Execute(new RenameCommand("nope.pdf"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*read-only*");
        session.DisplayName.Should().Be("locked.pdf");
        session.IsDirty.Should().BeFalse();
        session.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void Path_and_kind_are_retained()
    {
        var session = new DocumentSession(DocumentKind.Image, "photo.png", @"D:\photos\photo.png");
        session.Kind.Should().Be(DocumentKind.Image);
        session.Path.Should().Be(@"D:\photos\photo.png");
        session.DisplayName.Should().Be("photo.png");
        session.ViewState.Should().NotBeNull();
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
