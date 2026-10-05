using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PageInsertIndexTests
{
    [Fact]
    public void Append_equals_page_count()
    {
        PageInsertIndex.Append(5).Should().Be(5);
        PageInsertIndex.Prepend.Should().Be(0);
    }

    [Theory]
    [InlineData(-1, 3, 0)]
    [InlineData(0, 3, 0)]
    [InlineData(2, 3, 2)]
    [InlineData(99, 3, 3)]
    public void Clamp_insert_before(int index, int count, int expected)
    {
        PageInsertIndex.Clamp(index, count).Should().Be(expected);
    }
}
