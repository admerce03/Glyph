using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class PagePastePlacementTests
{
    [Fact]
    public void InsertAfterSelection_uses_max_selected_plus_one()
    {
        PagePastePlacement.InsertAfterSelection([1, 3], currentPageIndex: 0, pageCount: 10)
            .Should().Be(4);
    }

    [Fact]
    public void InsertAfterSelection_falls_back_to_current_page()
    {
        PagePastePlacement.InsertAfterSelection([], currentPageIndex: 2, pageCount: 10)
            .Should().Be(3);
    }

    [Fact]
    public void InsertAfterSelection_clamps_to_page_count()
    {
        PagePastePlacement.InsertAfterSelection([9], currentPageIndex: 0, pageCount: 10)
            .Should().Be(10);
    }
}
