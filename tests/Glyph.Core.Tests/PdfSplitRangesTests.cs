using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PdfSplitRangesTests
{
    [Fact]
    public void BuildRanges_with_no_splits_returns_single_range()
    {
        PdfSplitRanges.BuildRanges(4, [])
            .Should().BeEquivalentTo(new[] { new[] { 0, 1, 2, 3 } }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void BuildRanges_splits_before_indexes()
    {
        // pages 0..5, split before 2 and 4 → [0,1] [2,3] [4,5]
        var ranges = PdfSplitRanges.BuildRanges(6, [2, 4]);
        ranges.Should().HaveCount(3);
        ranges[0].Should().Equal(0, 1);
        ranges[1].Should().Equal(2, 3);
        ranges[2].Should().Equal(4, 5);
    }

    [Fact]
    public void BuildRanges_ignores_invalid_or_duplicate_starts()
    {
        PdfSplitRanges.BuildRanges(3, [0, 1, 1, 3, -1])
            .Should().BeEquivalentTo(new[] { new[] { 0 }, new[] { 1, 2 } }, opts => opts.WithStrictOrdering());
    }
}
