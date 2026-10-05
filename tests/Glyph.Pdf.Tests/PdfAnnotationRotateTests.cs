using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfAnnotationRotateTests
{
    [Fact]
    public void Rotate_point_90_clockwise_in_pdf_y_up()
    {
        var center = new PdfPagePoint(0, 0);
        var p = new PdfPagePoint(10, 0);
        var r = PdfAnnotationRotate.RotatePoint(p, center, 90);
        r.X.Should().BeApproximately(0, 1e-9);
        r.Y.Should().BeApproximately(-10, 1e-9);
    }

    [Fact]
    public void Rotate_bounds_90_swaps_aspect_around_center()
    {
        var bounds = new PdfRect(0, 0, 100, 40);
        var rotated = PdfAnnotationRotate.RotateBounds(bounds, 90);
        rotated.Width.Should().BeApproximately(40, 1e-9);
        rotated.Height.Should().BeApproximately(100, 1e-9);
        PdfAnnotationRotate.BoundsCenter(rotated).Should().Be(PdfAnnotationRotate.BoundsCenter(bounds));
    }

    [Fact]
    public void Rotate_bgra_90_swaps_dimensions()
    {
        // 2x1 image: red then blue pixels.
        var src = new byte[]
        {
            0, 0, 255, 255, // red (BGRA)
            255, 0, 0, 255, // blue
        };
        var dest = PdfAnnotationRotate.RotateBgra90Clockwise(src, width: 2, height: 1, out var w, out var h);
        w.Should().Be(1);
        h.Should().Be(2);
        // First source pixel (0,0) -> (0,0) wait: dx=h-1-y=0, dy=x=0 for (0,0)
        // Second (1,0) -> dx=0, dy=1
        dest[0].Should().Be(0);
        dest[1].Should().Be(0);
        dest[2].Should().Be(255);
        dest[4].Should().Be(255);
        dest[6].Should().Be(0);
    }
}
