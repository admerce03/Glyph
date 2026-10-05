using FluentAssertions;
using Glyph.Pdf.Text;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfSearchHitNavTests
{
    [Theory]
    [InlineData(0, 5, 0)]
    [InlineData(4, 5, 4)]
    [InlineData(5, 5, 0)]
    [InlineData(-1, 5, 4)]
    [InlineData(6, 5, 1)]
    [InlineData(-6, 5, 4)]
    public void WrapIndex_cycles(int hitIndex, int hitCount, int expected)
    {
        PdfSearchHitNav.WrapIndex(hitIndex, hitCount).Should().Be(expected);
    }

    [Fact]
    public void WrapIndex_empty_is_minus_one()
    {
        PdfSearchHitNav.WrapIndex(0, 0).Should().Be(-1);
        PdfSearchHitNav.WrapIndex(3, -1).Should().Be(-1);
    }

    [Fact]
    public void FormatStatus_is_one_based()
    {
        PdfSearchHitNav.FormatStatus(0, 3, pageIndex: 4).Should().Be("Match 1 / 3 · p.5");
    }
}
