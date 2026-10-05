using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Xunit;

namespace Glyph.Imaging.Tests;

public class ImageResizeDialogMathTests
{
    [Theory]
    [InlineData(0, ImageResizeFilter.Auto)]
    [InlineData(1, ImageResizeFilter.NearestNeighbor)]
    [InlineData(2, ImageResizeFilter.Bilinear)]
    [InlineData(3, ImageResizeFilter.Bicubic)]
    [InlineData(99, ImageResizeFilter.Bicubic)]
    [InlineData(-1, ImageResizeFilter.Auto)]
    public void FilterFromComboIndex_clamps(int index, ImageResizeFilter expected)
    {
        ImageResizeDialogMath.FilterFromComboIndex(index).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("Auto", 0)]
    [InlineData("NearestNeighbor", 1)]
    [InlineData("Bilinear", 2)]
    [InlineData("Bicubic", 3)]
    public void ComboIndexFromPreferenceName_maps(string? name, int expected)
    {
        ImageResizeDialogMath.ComboIndexFromPreferenceName(name).Should().Be(expected);
    }

    [Fact]
    public void EstimateRawBgraMegabytes_one_megapixel()
    {
        // 1024×1024×4 = 4 MiB
        ImageResizeDialogMath.EstimateRawBgraMegabytes(1024, 1024).Should().Be(4.0);
        ImageResizeDialogMath.EstimateRawBgraMegabytes(0, 10).Should().Be(0);
    }

    [Fact]
    public void Aspect_lock_and_percent_scale()
    {
        ImageResizeDialogMath.HeightForWidth(200, 2.0).Should().Be(100);
        ImageResizeDialogMath.WidthForHeight(100, 2.0).Should().Be(200);
        ImageResizeDialogMath.ScaleByPercent(100, 50, 50).Should().Be((50, 25));
        ImageResizeDialogMath.ScaleByPercent(100, 50, 200).Should().Be((200, 100));
    }
}
