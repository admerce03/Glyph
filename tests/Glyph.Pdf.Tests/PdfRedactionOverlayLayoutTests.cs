using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfRedactionOverlayLayoutTests
{
    [Fact]
    public void TryMapToCanvas_flips_y_and_scales()
    {
        // Page 792pt tall (Letter); mark near top of page in PDF space.
        var bounds = new PdfRect(72, 720, 172, 740);
        PdfRedactionOverlayLayout.TryMapToCanvas(bounds, pageHeightPoints: 792, scale: 2, out var canvas)
            .Should().BeTrue();

        canvas.Left.Should().Be(144);           // 72 * 2
        canvas.Top.Should().Be((792 - 740) * 2); // 104
        canvas.Width.Should().Be(200);          // 100 * 2
        canvas.Height.Should().Be(40);          // 20 * 2
    }

    [Fact]
    public void TryMapToCanvas_rejects_subpixel_rects()
    {
        var tiny = new PdfRect(0, 0, 0.2, 0.2);
        PdfRedactionOverlayLayout.TryMapToCanvas(tiny, pageHeightPoints: 792, scale: 1, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void TryMapToCanvas_rejects_nonpositive_scale()
    {
        var bounds = new PdfRect(0, 0, 100, 100);
        PdfRedactionOverlayLayout.TryMapToCanvas(bounds, pageHeightPoints: 792, scale: 0, out _)
            .Should().BeFalse();
    }
}
