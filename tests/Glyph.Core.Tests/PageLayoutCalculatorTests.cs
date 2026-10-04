using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PageLayoutCalculatorTests
{
    [Fact]
    public void Continuous_range_covers_all_pages()
    {
        PageLayoutCalculator.VisibleRange(PageLayoutMode.Continuous, 3, 10)
            .Should().Be((0, 9));
    }

    [Fact]
    public void Single_page_range_is_current_only()
    {
        PageLayoutCalculator.VisibleRange(PageLayoutMode.SinglePage, 3, 10)
            .Should().Be((3, 3));
    }

    [Fact]
    public void Two_page_range_pairs_even_odd()
    {
        PageLayoutCalculator.VisibleRange(PageLayoutMode.TwoPage, 2, 10)
            .Should().Be((2, 3));
        PageLayoutCalculator.VisibleRange(PageLayoutMode.TwoPage, 3, 10)
            .Should().Be((2, 3));
        PageLayoutCalculator.VisibleRange(PageLayoutMode.TwoPage, 9, 10)
            .Should().Be((8, 9));
    }

    [Fact]
    public void Two_page_navigation_steps_by_spread()
    {
        PageLayoutCalculator.NextPageIndex(PageLayoutMode.TwoPage, 2, 10).Should().Be(4);
        PageLayoutCalculator.PreviousPageIndex(PageLayoutMode.TwoPage, 4, 10).Should().Be(2);
        PageLayoutCalculator.NormalizePageIndex(PageLayoutMode.TwoPage, 3, 10).Should().Be(2);
    }
}
