using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfPageCoordinatesTests
{
    [Fact]
    public void FromDisplayPoint_flips_y()
    {
        var p = PdfPageCoordinates.FromDisplayPoint(100, 50, pageHeightPoints: 792, scale: 2);
        p.X.Should().Be(50);
        p.Y.Should().Be(792 - 25);
    }

    [Fact]
    public void FromDisplayRect_normalizes_and_flips()
    {
        // UI: top-left (10,20) to bottom-right (110,120), scale 1, page 792
        var rect = PdfPageCoordinates.FromDisplayRect(10, 20, 110, 120, 792, 1);
        rect.Left.Should().Be(10);
        rect.Right.Should().Be(110);
        rect.Bottom.Should().Be(792 - 120);
        rect.Top.Should().Be(792 - 20);
        rect.Height.Should().Be(100);
        rect.Width.Should().Be(100);
    }

    [Fact]
    public void FromDisplayRect_inverted_drag_corners_still_normalized()
    {
        var rect = PdfPageCoordinates.FromDisplayRect(110, 120, 10, 20, 792, 1);
        rect.Left.Should().Be(10);
        rect.Right.Should().Be(110);
        rect.Bottom.Should().BeLessThan(rect.Top);
        rect.Intersects(new PdfRect(50, 700, 60, 780)).Should().BeTrue();
    }

    [Fact]
    public void FromDisplayRectXywh_overload()
    {
        var rect = PdfPageCoordinates.FromDisplayRectXywh(
            displayX: 10,
            displayY: 20,
            displayWidth: 100,
            displayHeight: 100,
            pageHeightPoints: 792,
            scale: 1);
        rect.Should().Be(PdfPageCoordinates.FromDisplayRect(10, 20, 110, 120, 792, 1));
    }
}
