using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfCropBoxTests
{
    [Fact]
    public void Width_Height_and_IsValid()
    {
        var box = new PdfCropBox(10, 20, 110, 220);
        box.Width.Should().Be(100);
        box.Height.Should().Be(200);
        box.IsValid.Should().BeTrue();

        new PdfCropBox(10, 20, 10, 220).IsValid.Should().BeFalse();
        new PdfCropBox(10, 20, 110, 20).IsValid.Should().BeFalse();
        new PdfCropBox(110, 20, 10, 220).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Uniform_margins_match_all_sides()
    {
        var m = PdfCropMargins.Uniform(12);
        m.LeftPoints.Should().Be(12);
        m.TopPoints.Should().Be(12);
        m.RightPoints.Should().Be(12);
        m.BottomPoints.Should().Be(12);
    }

    [Fact]
    public void ClampMargin_keeps_min_remaining_extent()
    {
        // page 100, opposite 20, minSize 30 → max margin = 50
        PdfCropMargins.ClampMargin(80, 100, 20, 30).Should().Be(50);
        PdfCropMargins.ClampMargin(-5, 100, 20, 30).Should().Be(0);
        PdfCropMargins.ClampMargin(10, 100, 20, 30).Should().Be(10);
    }

    [Fact]
    public void ClampMargin_when_opposite_consumes_page_returns_zero()
    {
        PdfCropMargins.ClampMargin(10, 50, 40, 20).Should().Be(0);
    }
}
