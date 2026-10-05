using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Xunit;

namespace Glyph.Imaging.Tests;

public class ImageZoomCalculatorTests
{
    [Fact]
    public void Clamp_respects_min_and_max()
    {
        ImageZoomCalculator.Clamp(0.001).Should().Be(ImageZoomCalculator.MinScale);
        ImageZoomCalculator.Clamp(50).Should().Be(ImageZoomCalculator.MaxScale);
        ImageZoomCalculator.Clamp(1.5).Should().Be(1.5);
    }

    [Fact]
    public void Zoom_in_out_use_step()
    {
        ImageZoomCalculator.ZoomIn(1.0).Should().Be(ImageZoomCalculator.ZoomStep);
        ImageZoomCalculator.ZoomOut(ImageZoomCalculator.ZoomStep).Should().Be(1.0);
    }

    [Fact]
    public void Wheel_zoom_uses_finer_step()
    {
        ImageZoomCalculator.ApplyWheelZoom(1.0, 120)
            .Should().BeApproximately(ImageZoomCalculator.WheelStep, 0.0001);
        ImageZoomCalculator.ApplyWheelZoom(1.0, -120)
            .Should().BeApproximately(1.0 / ImageZoomCalculator.WheelStep, 0.0001);
    }

    [Fact]
    public void Manipulation_scale_clamps()
    {
        ImageZoomCalculator.ApplyManipulationScale(1.0, 1.2).Should().BeApproximately(1.2, 0.0001);
        ImageZoomCalculator.ApplyManipulationScale(1.0, 0.001).Should().Be(ImageZoomCalculator.MinScale);
        ImageZoomCalculator.ApplyManipulationScale(1.0, 50).Should().Be(ImageZoomCalculator.MaxScale);
    }

    [Fact]
    public void Fit_picks_limiting_dimension()
    {
        // 800×600 viewport, 24 pad → 776×576 usable; 2000×1000 image → width-limited 0.388
        var scale = ImageZoomCalculator.Fit(800, 600, pixelWidth: 2000, pixelHeight: 1000);
        scale.Should().BeApproximately(776.0 / 2000.0, 0.0001);
    }

    [Fact]
    public void ActualSizePrint_scales_by_dpi_ratio()
    {
        ImageZoomCalculator.ActualSizePrint(screenDpi: 192, imageDpi: 96).Should().Be(2.0);
        ImageZoomCalculator.ActualSizePrint(96, 0).Should().Be(ImageZoomCalculator.MaxScale);
    }

    [Fact]
    public void DecodeTargetEdge_clamps()
    {
        ImageZoomCalculator.DecodeTargetEdge(100, 1.0).Should().Be(ImageZoomCalculator.MinDecodeEdge);
        ImageZoomCalculator.DecodeTargetEdge(20000, 1.0).Should().Be(ImageZoomCalculator.MaxDecodeEdge);
        ImageZoomCalculator.DecodeTargetEdge(1000, 2.0).Should().Be(2000);
    }

    [Fact]
    public void NeedsProgressivePreview_for_large_rasters()
    {
        ImageZoomCalculator.NeedsProgressivePreview(5000, 4000).Should().BeTrue();
        ImageZoomCalculator.NeedsProgressivePreview(1000, 800).Should().BeFalse();
        ImageZoomCalculator.NeedsProgressivePreview(5000, 1000).Should().BeFalse();
    }
}
