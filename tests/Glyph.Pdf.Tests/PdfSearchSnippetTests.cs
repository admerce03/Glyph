using FluentAssertions;
using Glyph.Pdf.Text;

namespace Glyph.Pdf.Tests;

public class PdfSearchSnippetTests
{
    [Fact]
    public void Build_pads_and_ellipsis_middle_match()
    {
        var text = new string('a', 80) + "NEEDLE" + new string('b', 80);
        var snippet = PdfSearchSnippet.Build(text, 80, 6, pad: 4);
        snippet.Should().StartWith("…");
        snippet.Should().EndWith("…");
        snippet.Should().Contain("NEEDLE");
    }

    [Fact]
    public void Build_no_leading_ellipsis_at_start()
    {
        var snippet = PdfSearchSnippet.Build("hello world", 0, 5, pad: 2);
        snippet.Should().NotStartWith("…");
        snippet.Should().StartWith("hello");
    }

    [Fact]
    public void Build_normalizes_newlines_to_spaces()
    {
        PdfSearchSnippet.Build("ab\ncd\ref", 0, 8, pad: 0).Should().Be("ab cd ef");
    }

    [Theory]
    [InlineData("", 0, 1)]
    [InlineData("abc", 3, 1)]
    [InlineData("abc", 0, 0)]
    public void Build_empty_for_invalid_inputs(string text, int start, int len)
    {
        PdfSearchSnippet.Build(text, start, len).Should().BeEmpty();
    }
}
