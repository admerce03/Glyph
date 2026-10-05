using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.Pdf;

namespace Glyph.Core.Tests;

public class ThumbnailContextMenuTests
{
    [Theory]
    [InlineData(1, "Rotate left", "Delete")]
    [InlineData(3, "Rotate left (3)", "Delete (3)")]
    public void Labels_reflect_selection_count(int count, string rotate, string delete)
    {
        ThumbnailContextMenu.RotateLeft(count).Should().Be(rotate);
        ThumbnailContextMenu.Delete(count).Should().Be(delete);
        ThumbnailContextMenu.InsertBlankAfter.Should().Contain("blank");
    }
}

public class PdfLinkActionTests
{
    [Theory]
    [InlineData(0, null, PdfLinkAction.Kind.GoToPage)]
    [InlineData(null, "https://example.com", PdfLinkAction.Kind.OpenUri)]
    [InlineData(null, null, PdfLinkAction.Kind.None)]
    [InlineData(-1, "https://example.com", PdfLinkAction.Kind.OpenUri)]
    public void Resolve_prefers_internal_page(int? page, string? uri, PdfLinkAction.Kind expected)
    {
        PdfLinkAction.Resolve(page, uri).Should().Be(expected);
    }

    [Fact]
    public void OutlineNavigation_requires_non_negative_page()
    {
        OutlineNavigation.TryGetPageIndex(2, out var page).Should().BeTrue();
        page.Should().Be(2);
        OutlineNavigation.TryGetPageIndex(null, out _).Should().BeFalse();
        OutlineNavigation.TryGetPageIndex(-1, out _).Should().BeFalse();
    }
}
