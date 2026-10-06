using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class FindOptionsPolicyTests
{
    [Fact]
    public void SortComboIndex_maps_relevance_flag()
    {
        FindOptionsPolicy.SortComboIndex(false).Should().Be(FindOptionsPolicy.SortPageOrderIndex);
        FindOptionsPolicy.SortComboIndex(true).Should().Be(FindOptionsPolicy.SortRelevanceIndex);
        FindOptionsPolicy.SortByRelevanceFromIndex(0).Should().BeFalse();
        FindOptionsPolicy.SortByRelevanceFromIndex(1).Should().BeTrue();
        FindOptionsPolicy.SortByRelevanceFromIndex(99).Should().BeFalse();
    }

    [Fact]
    public void ApplyTo_copies_flags_and_sort_index()
    {
        FindOptionsPolicy.ApplyTo(
            caseSensitive: true,
            anyWord: true,
            sortByRelevance: true,
            out var caseOut,
            out var anyOut,
            out var sortIndex);
        caseOut.Should().BeTrue();
        anyOut.Should().BeTrue();
        sortIndex.Should().Be(FindOptionsPolicy.SortRelevanceIndex);
    }
}
