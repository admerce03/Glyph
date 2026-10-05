using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PageLayoutComboTests
{
    [Theory]
    [InlineData(0, PageLayoutMode.Continuous)]
    [InlineData(1, PageLayoutMode.SinglePage)]
    [InlineData(2, PageLayoutMode.TwoPage)]
    [InlineData(3, PageLayoutMode.TwoPageWithCover)]
    [InlineData(4, PageLayoutMode.Continuous)] // contact sheet is not a PageLayoutMode
    [InlineData(-1, PageLayoutMode.Continuous)]
    public void FromComboIndex_maps_known_indexes(int index, PageLayoutMode expected)
    {
        PageLayoutCombo.FromComboIndex(index).Should().Be(expected);
    }

    [Theory]
    [InlineData(PageLayoutMode.Continuous, 0)]
    [InlineData(PageLayoutMode.SinglePage, 1)]
    [InlineData(PageLayoutMode.TwoPage, 2)]
    [InlineData(PageLayoutMode.TwoPageWithCover, 3)]
    public void ToComboIndex_round_trips(PageLayoutMode mode, int expected)
    {
        PageLayoutCombo.ToComboIndex(mode).Should().Be(expected);
        PageLayoutCombo.FromComboIndex(expected).Should().Be(mode);
    }
}
