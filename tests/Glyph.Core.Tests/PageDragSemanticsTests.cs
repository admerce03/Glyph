using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PageDragSemanticsTests
{
    [Theory]
    [InlineData(null, "doc-a", true)]
    [InlineData("", "doc-a", true)]
    [InlineData("doc-a", "doc-a", true)]
    [InlineData("doc-b", "doc-a", false)]
    public void IsSameDocument(string? payloadKey, string dropKey, bool expected)
    {
        PageDragSemantics.IsSameDocument(payloadKey, dropKey).Should().Be(expected);
    }

    [Fact]
    public void DocumentKey_prefers_path()
    {
        PageDragSemantics.DocumentKey(@"C:\docs\a.pdf", 0xABC).Should().Be(@"C:\docs\a.pdf");
        PageDragSemantics.DocumentKey(null, 0xABC).Should().Be("ABC");
        PageDragSemantics.DocumentKey("  ", 255).Should().Be("FF");
    }
}
