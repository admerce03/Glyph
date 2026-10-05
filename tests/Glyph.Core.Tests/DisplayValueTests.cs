using FluentAssertions;
using Glyph.Core.Text;

namespace Glyph.Core.Tests;

public class DisplayValueTests
{
    [Theory]
    [InlineData(null, "—")]
    [InlineData("", "—")]
    [InlineData("   ", "—")]
    [InlineData("Title", "Title")]
    public void OrEmDash_empty_becomes_placeholder(string? value, string expected)
    {
        DisplayValue.OrEmDash(value).Should().Be(expected);
    }
}
