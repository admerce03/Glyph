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
    public void Two_page_with_cover_shows_first_page_alone()
    {
        PageLayoutCalculator.VisibleRange(PageLayoutMode.TwoPageWithCover, 0, 10)
            .Should().Be((0, 0));
        PageLayoutCalculator.VisibleRange(PageLayoutMode.TwoPageWithCover, 1, 10)
            .Should().Be((1, 2));
        PageLayoutCalculator.VisibleRange(PageLayoutMode.TwoPageWithCover, 2, 10)
            .Should().Be((1, 2));
        PageLayoutCalculator.VisibleRange(PageLayoutMode.TwoPageWithCover, 3, 10)
            .Should().Be((3, 4));
    }

    [Fact]
    public void Two_page_navigation_steps_by_spread()
    {
        PageLayoutCalculator.NextPageIndex(PageLayoutMode.TwoPage, 2, 10).Should().Be(4);
        PageLayoutCalculator.PreviousPageIndex(PageLayoutMode.TwoPage, 4, 10).Should().Be(2);
        PageLayoutCalculator.NormalizePageIndex(PageLayoutMode.TwoPage, 3, 10).Should().Be(2);
    }

    [Fact]
    public void Cover_mode_navigation_steps_from_cover_then_by_spread()
    {
        PageLayoutCalculator.NextPageIndex(PageLayoutMode.TwoPageWithCover, 0, 10).Should().Be(1);
        PageLayoutCalculator.NextPageIndex(PageLayoutMode.TwoPageWithCover, 1, 10).Should().Be(3);
        PageLayoutCalculator.PreviousPageIndex(PageLayoutMode.TwoPageWithCover, 1, 10).Should().Be(0);
        PageLayoutCalculator.PreviousPageIndex(PageLayoutMode.TwoPageWithCover, 3, 10).Should().Be(1);
        PageLayoutCalculator.NormalizePageIndex(PageLayoutMode.TwoPageWithCover, 2, 10).Should().Be(1);
    }

    [Fact]
    public void First_and_last_page_index_when_empty()
    {
        PageLayoutCalculator.FirstPageIndex(PageLayoutMode.SinglePage, 0).Should().Be(0);
        PageLayoutCalculator.LastPageIndex(PageLayoutMode.SinglePage, 0).Should().Be(0);
    }

    [Fact]
    public void First_and_last_page_index_for_single_and_continuous()
    {
        PageLayoutCalculator.FirstPageIndex(PageLayoutMode.SinglePage, 10).Should().Be(0);
        PageLayoutCalculator.LastPageIndex(PageLayoutMode.SinglePage, 10).Should().Be(9);
        PageLayoutCalculator.FirstPageIndex(PageLayoutMode.Continuous, 10).Should().Be(0);
        PageLayoutCalculator.LastPageIndex(PageLayoutMode.Continuous, 10).Should().Be(9);
    }

    [Fact]
    public void Last_page_index_normalizes_to_spread_left_in_facing_modes()
    {
        PageLayoutCalculator.FirstPageIndex(PageLayoutMode.TwoPage, 10).Should().Be(0);
        PageLayoutCalculator.LastPageIndex(PageLayoutMode.TwoPage, 10).Should().Be(8);
        PageLayoutCalculator.LastPageIndex(PageLayoutMode.TwoPageWithCover, 10).Should().Be(8);
    }
}
