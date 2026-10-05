using FluentAssertions;
using Glyph.Core.Documents;

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

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    public void PreferCopyOnly_for_ctrl_or_storage_insert(bool ctrl, bool storageOnly, bool expected)
    {
        PageDropPlacement.PreferCopyOnly(ctrl, storageOnly).Should().Be(expected);
    }

    [Fact]
    public void HighlightThickness_emphasizes_insert_edge()
    {
        PageDropPlacement.HighlightThickness(insertAfter: true).Should().Be((2, 2, 2, 5));
        PageDropPlacement.HighlightThickness(insertAfter: false).Should().Be((2, 5, 2, 2));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void AcceptsDrop_when_text_or_storage(bool text, bool storage, bool expected)
    {
        PageDropPlacement.AcceptsDrop(text, storage).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    public void IsStorageInsertOnly(bool storage, bool text, bool expected)
    {
        PageDropPlacement.IsStorageInsertOnly(storage, text).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, false, false)]
    public void PreferCopyOperation_matches_ctrl_and_storage_insert(
        bool ctrl, bool storage, bool text, bool expected)
    {
        PageDropPlacement.PreferCopyOperation(ctrl, storage, text).Should().Be(expected);
    }
}
