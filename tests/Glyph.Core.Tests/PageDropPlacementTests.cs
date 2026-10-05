using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class PageDropPlacementTests
{
    [Theory]
    [InlineData(0, 100, false)]
    [InlineData(49, 100, false)]
    [InlineData(50.1, 100, true)]
    [InlineData(100, 100, true)]
    public void IsInsertAfter_uses_lower_half(double y, double height, bool expected)
    {
        PageDropPlacement.IsInsertAfter(y, height).Should().Be(expected);
    }

    [Fact]
    public void IsInsertAfter_zero_height_defaults_after()
    {
        PageDropPlacement.IsInsertAfter(0, 0).Should().BeTrue();
    }

    [Theory]
    [InlineData(true, false, "Insert PDF pages")]
    [InlineData(true, true, "Move or copy pages here")]
    [InlineData(false, true, "Move or copy pages here")]
    [InlineData(false, false, "Move or copy pages here")]
    public void Caption_distinguishes_storage_insert(bool storage, bool text, string expected)
    {
        PageDropPlacement.Caption(storage, text).Should().Be(expected);
    }
}
