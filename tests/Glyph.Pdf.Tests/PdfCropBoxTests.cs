using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Xunit;

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
}
