using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageCropAspectTests
{
    [Fact]
    public void Constrain_free_mode_uses_raw_rectangle()
    {
        var (x, y, w, h) = ImageCropAspect.Constrain(10, 20, 50, 80, 200, 200, aspectWidthOverHeight: null);
        x.Should().Be(10);
        y.Should().Be(20);
        w.Should().Be(40);
        h.Should().Be(60);
    }

    [Fact]
    public void Constrain_square_keeps_1_to_1()
    {
        var (x, y, w, h) = ImageCropAspect.Constrain(0, 0, 80, 40, 200, 200, aspectWidthOverHeight: 1.0);
        w.Should().BeApproximately(h, 0.01);
        w.Should().BeGreaterThan(0);
        x.Should().Be(0);
        y.Should().Be(0);
    }

    [Fact]
    public void Constrain_sixteen_nine_matches_ratio()
    {
        var (_, _, w, h) = ImageCropAspect.Constrain(0, 0, 160, 20, 400, 400, aspectWidthOverHeight: 16.0 / 9.0);
        (w / h).Should().BeApproximately(16.0 / 9.0, 0.02);
    }

    [Fact]
    public void Constrain_clamps_to_display_bounds()
    {
        var (x, y, w, h) = ImageCropAspect.Constrain(90, 90, 200, 200, 100, 100, aspectWidthOverHeight: 1.0);
        x.Should().BeGreaterThanOrEqualTo(0);
        y.Should().BeGreaterThanOrEqualTo(0);
        (x + w).Should().BeLessThanOrEqualTo(100.01);
        (y + h).Should().BeLessThanOrEqualTo(100.01);
    }
}
