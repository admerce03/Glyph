using FluentAssertions;
using Glyph.Pdf.Text;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfSearchStatusTextTests
{
    [Theory]
    [InlineData(PdfSearchStatus.EmptyQuery, "Enter search text.")]
    [InlineData(PdfSearchStatus.NoMatches, "No matches.")]
    [InlineData(PdfSearchStatus.NoExtractableText, "OCR required.")]
    [InlineData(PdfSearchStatus.DocumentEncrypted, "Password required.")]
    [InlineData(PdfSearchStatus.Failed, "Search failed.")]
    [InlineData(PdfSearchStatus.Cancelled, "Search cancelled.")]
    public void Format_defaults(PdfSearchStatus status, string expected)
    {
        PdfSearchStatusText.Format(status).Should().Be(expected);
    }

    [Fact]
    public void Format_success_pluralizes()
    {
        PdfSearchStatusText.Format(PdfSearchStatus.Success, matchCount: 1).Should().Be("1 match");
        PdfSearchStatusText.Format(PdfSearchStatus.Success, matchCount: 3).Should().Be("3 matches");
    }

    [Fact]
    public void Format_prefers_explicit_message()
    {
        PdfSearchStatusText.Format(PdfSearchStatus.Failed, message: "boom").Should().Be("boom");
    }
}
