using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.Workspace;

namespace Glyph.Core.Tests;

public class WorkspaceReuseTests
{
    [Fact]
    public void Open_reuses_existing_session_for_same_path()
    {
        var workspace = new WorkspaceState();
        var path = Path.Combine(Path.GetTempPath(), "glyph-reuse.pdf");

        var first = workspace.Open(DocumentKind.Pdf, "glyph-reuse.pdf", path);
        var second = workspace.Open(DocumentKind.Pdf, "glyph-reuse.pdf", path);

        second.Should().BeSameAs(first);
        workspace.Documents.Should().HaveCount(1);
    }

    [Fact]
    public void ActivateNext_and_Previous_cycle_tabs()
    {
        var workspace = new WorkspaceState();
        var a = workspace.Open(DocumentKind.Pdf, "a.pdf", Path.Combine(Path.GetTempPath(), "a.pdf"));
        var b = workspace.Open(DocumentKind.Pdf, "b.pdf", Path.Combine(Path.GetTempPath(), "b.pdf"));
        var c = workspace.Open(DocumentKind.Pdf, "c.pdf", Path.Combine(Path.GetTempPath(), "c.pdf"));

        workspace.Activate(a.Id);
        workspace.ActivateNext().Should().BeTrue();
        workspace.ActiveDocument.Should().BeSameAs(b);
        workspace.ActivatePrevious().Should().BeTrue();
        workspace.ActiveDocument.Should().BeSameAs(a);
        workspace.ActivatePrevious().Should().BeTrue();
        workspace.ActiveDocument.Should().BeSameAs(c);
    }

    [Fact]
    public void CloseAll_clears_workspace()
    {
        var workspace = new WorkspaceState();
        workspace.Open(DocumentKind.Pdf, "a.pdf");
        workspace.Open(DocumentKind.Image, "b.png");

        var closed = workspace.CloseAll();
        closed.Should().HaveCount(2);
        workspace.Documents.Should().BeEmpty();
        workspace.ActiveDocument.Should().BeNull();
    }
}
