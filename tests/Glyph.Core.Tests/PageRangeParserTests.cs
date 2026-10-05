using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class PageRangeParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void Parse_blank_or_invalid_returns_empty(string? text)
    {
        PageRangeParser.Parse(text, 10).Should().BeEmpty();
    }

    [Fact]
    public void Parse_zero_page_count_returns_empty()
    {
        PageRangeParser.Parse("1-3", 0).Should().BeEmpty();
        PageRangeParser.Parse("1-3", -1).Should().BeEmpty();
    }

    [Fact]
    public void Parse_single_pages()
    {
        PageRangeParser.Parse("1,3,5", 5).Should().Equal(0, 2, 4);
    }

    [Fact]
    public void Parse_range_inclusive()
    {
        PageRangeParser.Parse("1-3", 10).Should().Equal(0, 1, 2);
    }

    [Fact]
    public void Parse_swaps_inverted_bounds()
    {
        PageRangeParser.Parse("3-1", 10).Should().Equal(0, 1, 2);
    }

    [Fact]
    public void Parse_mixed_range_and_singles_sorted_unique()
    {
        PageRangeParser.Parse("1-3,5,2", 10).Should().Equal(0, 1, 2, 4);
    }

    [Fact]
    public void Parse_drops_out_of_range()
    {
        PageRangeParser.Parse("0,1,99,3", 3).Should().Equal(0, 2);
    }

    [Fact]
    public void Parse_ignores_malformed_segments()
    {
        PageRangeParser.Parse("1,foo,2-x,3", 5).Should().Equal(0, 2);
    }
}
