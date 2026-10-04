using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PdfZoomCalculatorTests
{
    [Fact]
    public void ActualSize_is_one()
    {
        PdfZoomCalculator.ActualSize().Should().Be(1.0);
    }

    [Fact]
    public void FitWidth_uses_viewport_minus_padding()
    {
        // usable = 776, page = 612 → ~1.26797
        var scale = PdfZoomCalculator.FitWidth(viewportWidthDip: 800, pageWidthPoints: 612, horizontalPadding: 24);
        scale.Should().BeApproximately(776.0 / 612.0, 0.0001);
    }

    [Fact]
    public void FitPage_picks_the_limiting_dimension()
    {
        var widthLimited = PdfZoomCalculator.FitPage(400, 2000, pageWidthPoints: 612, pageHeightPoints: 792);
        var heightLimited = PdfZoomCalculator.FitPage(2000, 400, pageWidthPoints: 612, pageHeightPoints: 792);

        widthLimited.Should().BeApproximately((400 - 24) / 612.0, 0.0001);
        heightLimited.Should().BeApproximately((400 - 24) / 792.0, 0.0001);
        heightLimited.Should().BeLessThan(widthLimited);
    }

    [Fact]
    public void Wheel_zoom_steps_in_and_out()
    {
        PdfZoomCalculator.ApplyWheelZoom(1.0, wheelDelta: 120).Should().Be(PdfZoomCalculator.ZoomIn(1.0));
        PdfZoomCalculator.ApplyWheelZoom(1.0, wheelDelta: -120).Should().Be(PdfZoomCalculator.ZoomOut(1.0));
    }

    [Fact]
    public void Clamp_respects_min_and_max()
    {
        PdfZoomCalculator.Clamp(0.01).Should().Be(PdfZoomCalculator.MinScale);
        PdfZoomCalculator.Clamp(50).Should().Be(PdfZoomCalculator.MaxScale);
    }
}
