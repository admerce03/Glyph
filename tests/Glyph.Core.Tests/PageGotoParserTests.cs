using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PageGotoParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("11")]
    public void TryParseZeroBased_rejects_invalid(string? text)
    {
        PageGotoParser.TryParseZeroBased(text, pageCount: 10).Should().BeNull();
    }

    [Fact]
    public void TryParseZeroBased_rejects_empty_document()
    {
        PageGotoParser.TryParseZeroBased("1", pageCount: 0).Should().BeNull();
    }

    [Theory]
    [InlineData("1", 10, 0)]
    [InlineData("10", 10, 9)]
    [InlineData(" 5 ", 10, 4)]
    public void TryParseZeroBased_valid_one_based(string text, int pageCount, int expected)
    {
        PageGotoParser.TryParseZeroBased(text, pageCount).Should().Be(expected);
    }
}
