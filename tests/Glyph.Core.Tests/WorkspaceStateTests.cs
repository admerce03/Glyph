using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.Workspace;

namespace Glyph.Core.Tests;

public class WorkspaceStateTests
{
    [Fact]
    public void Open_sets_active_document_and_tracks_tabs()
    {
        var workspace = new WorkspaceState();

        var first = workspace.Open(DocumentKind.Pdf, "a.pdf", @"C:\a.pdf");
        var second = workspace.Open(DocumentKind.Image, "b.png", @"C:\b.png");

        workspace.Documents.Should().HaveCount(2);
        workspace.ActiveDocument.Should().BeSameAs(second);

        workspace.Activate(first.Id).Should().BeTrue();
        workspace.ActiveDocument.Should().BeSameAs(first);
    }

    [Fact]
    public void Close_selects_neighboring_document()
    {
        var workspace = new WorkspaceState();
        var a = workspace.Open(DocumentKind.Pdf, "a.pdf");
        var b = workspace.Open(DocumentKind.Pdf, "b.pdf");
        var c = workspace.Open(DocumentKind.Pdf, "c.pdf");

        workspace.Activate(b.Id);
        workspace.Close(b.Id).Should().BeTrue();

        workspace.Documents.Should().Equal(a, c);
        workspace.ActiveDocument.Should().BeSameAs(c);
    }

    [Fact]
    public void Reorder_updates_document_list_to_match_tab_order()
    {
        var workspace = new WorkspaceState();
        var a = workspace.Open(DocumentKind.Pdf, "a.pdf");
        var b = workspace.Open(DocumentKind.Pdf, "b.pdf");
        var c = workspace.Open(DocumentKind.Pdf, "c.pdf");

        workspace.Reorder([c.Id, a.Id, b.Id]);

        workspace.Documents.Should().Equal(c, a, b);
        workspace.ActiveDocument.Should().BeSameAs(c);
    }
}
