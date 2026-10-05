using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PageDragDisplayTests
{
    [Theory]
    [InlineData(1, "PDF page")]
    [InlineData(3, "3 PDF pages")]
    public void DragTitle(int count, string expected)
    {
        PageDragDisplay.DragTitle(count).Should().Be(expected);
    }
}
