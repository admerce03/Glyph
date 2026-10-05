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

    [Fact]
    public void Endpoint_p1_moves_end_only()
    {
        var a = new PdfPagePoint(10, 20);
        var b = new PdfPagePoint(100, 80);
        var (na, nb) = PdfAnnotationResize.ComputeEndpoints(a, b, "p1", deltaXPoints: 5, deltaYPoints: -10);
        na.Should().Be(a);
        nb.X.Should().Be(105);
        nb.Y.Should().Be(70);
    }

    [Fact]
    public void Endpoint_collapse_is_rejected()
    {
        var a = new PdfPagePoint(0, 0);
        var b = new PdfPagePoint(1, 0);
        var (na, nb) = PdfAnnotationResize.ComputeEndpoints(a, b, "p1", deltaXPoints: -1, deltaYPoints: 0);
        na.Should().Be(a);
        nb.Should().Be(b);
    }

    [Fact]
    public void IsEndpointHandle_recognizes_p0_p1()
    {
        PdfAnnotationResize.IsEndpointHandle("p0").Should().BeTrue();
        PdfAnnotationResize.IsEndpointHandle("e").Should().BeFalse();
    }
}
