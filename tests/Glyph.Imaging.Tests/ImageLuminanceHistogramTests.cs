using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageLuminanceHistogramTests
{
    [Fact]
    public void BuildBins_empty_is_zeros()
    {
        ImageLuminanceHistogram.BuildBins([], 8).Should().Equal(0, 0, 0, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void BuildBins_black_and_white_pixels()
    {
        // BGRA: black then white
        var bgra = new byte[]
        {
            0, 0, 0, 255,
            255, 255, 255, 255,
        };
        var bins = ImageLuminanceHistogram.BuildBins(bgra, binCount: 4);
        bins.Sum().Should().Be(2);
        bins[0].Should().Be(1); // black
        bins[3].Should().Be(1); // white
    }

    [Fact]
    public void BuildBins_rejects_nonpositive_count()
    {
        var act = () => ImageLuminanceHistogram.BuildBins([], 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
