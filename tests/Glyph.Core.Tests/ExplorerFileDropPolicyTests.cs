using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class ExplorerFileDropPolicyTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void ShouldOpenDroppedFiles(bool hasStorage, bool pageDrag, bool expected)
    {
        ExplorerFileDropPolicy.ShouldOpenDroppedFiles(hasStorage, pageDrag).Should().Be(expected);
    }

    [Fact]
    public void DragCaption_is_open_in_glyph()
    {
        ExplorerFileDropPolicy.DragCaption.Should().Be("Open in Glyph");
    }
}
