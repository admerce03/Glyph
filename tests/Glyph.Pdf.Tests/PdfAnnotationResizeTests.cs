using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Editing;

namespace Glyph.Pdf.Tests;

public class PdfAnnotationResizeTests
{
    [Fact]
    public void East_handle_grows_width_keeping_left()
    {
        var origin = new PdfRect(10, 20, 40, 50);
        var next = PdfAnnotationResize.ComputeBounds(origin, "e", deltaXPoints: 15, deltaYPoints: 0);
        next.Left.Should().Be(10);
        next.Right.Should().Be(55);
        next.Bottom.Should().Be(20);
        next.Top.Should().Be(50);
    }

    [Fact]
    public void Northwest_handle_moves_left_and_top()
    {
        var origin = new PdfRect(100, 200, 160, 260);
        var next = PdfAnnotationResize.ComputeBounds(origin, "nw", deltaXPoints: -10, deltaYPoints: 20);
        next.Left.Should().Be(90);
        next.Top.Should().Be(280);
        next.Right.Should().Be(160);
        next.Bottom.Should().Be(200);
    }

    [Fact]
    public void Min_size_clamps_collapse()
    {
        var origin = new PdfRect(0, 0, 50, 50);
        var next = PdfAnnotationResize.ComputeBounds(origin, "e", deltaXPoints: -100, deltaYPoints: 0);
        next.Width.Should().Be(PdfAnnotationResize.DefaultMinSizePoints);
        next.Left.Should().Be(0);
    }
}
