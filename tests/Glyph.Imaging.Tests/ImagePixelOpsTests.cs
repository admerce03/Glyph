using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImagePixelOpsTests
{
    [Fact]
    public void ApplyGrayscale_makes_channels_equal()
    {
        var bgra = new byte[] { 10, 20, 30, 255, 0, 128, 255, 200 };
        ImagePixelOps.ApplyGrayscale(bgra);
        bgra[0].Should().Be(bgra[1]);
        bgra[1].Should().Be(bgra[2]);
        bgra[3].Should().Be(255);
        bgra[4].Should().Be(bgra[5]);
        bgra[5].Should().Be(bgra[6]);
        bgra[7].Should().Be(200);
    }
}
