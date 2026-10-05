using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Xunit;

namespace Glyph.Imaging.Tests;

public class ImageAdjustmentsTests
{
    [Fact]
    public void Default_is_identity()
    {
        new ImageAdjustments().IsIdentity.Should().BeTrue();
    }

    [Theory]
    [InlineData(nameof(ImageAdjustments.Brightness))]
    [InlineData(nameof(ImageAdjustments.Contrast))]
    [InlineData(nameof(ImageAdjustments.Saturation))]
    [InlineData(nameof(ImageAdjustments.Sharpness))]
    [InlineData(nameof(ImageAdjustments.Temperature))]
    [InlineData(nameof(ImageAdjustments.Tint))]
    [InlineData(nameof(ImageAdjustments.Highlights))]
    [InlineData(nameof(ImageAdjustments.Shadows))]
    [InlineData(nameof(ImageAdjustments.BlackPoint))]
    public void Nonzero_slider_breaks_identity(string property)
    {
        var adj = property switch
        {
            nameof(ImageAdjustments.Brightness) => new ImageAdjustments(Brightness: 1),
            nameof(ImageAdjustments.Contrast) => new ImageAdjustments(Contrast: 1),
            nameof(ImageAdjustments.Saturation) => new ImageAdjustments(Saturation: 1),
            nameof(ImageAdjustments.Sharpness) => new ImageAdjustments(Sharpness: 1),
            nameof(ImageAdjustments.Temperature) => new ImageAdjustments(Temperature: 1),
            nameof(ImageAdjustments.Tint) => new ImageAdjustments(Tint: 1),
            nameof(ImageAdjustments.Highlights) => new ImageAdjustments(Highlights: 1),
            nameof(ImageAdjustments.Shadows) => new ImageAdjustments(Shadows: 1),
            nameof(ImageAdjustments.BlackPoint) => new ImageAdjustments(BlackPoint: 1),
            _ => throw new ArgumentOutOfRangeException(nameof(property)),
        };
        adj.IsIdentity.Should().BeFalse();
    }

    [Fact]
    public void Flags_and_whitepoint_gamma_break_or_keep_identity()
    {
        new ImageAdjustments(AutoLevels: true).IsIdentity.Should().BeFalse();
        new ImageAdjustments(Sepia: true).IsIdentity.Should().BeFalse();
        new ImageAdjustments(WhitePoint: 90).IsIdentity.Should().BeFalse();
        new ImageAdjustments(Gamma: 1.2).IsIdentity.Should().BeFalse();
        new ImageAdjustments(WhitePoint: 100, Gamma: 1.0).IsIdentity.Should().BeTrue();
    }
}
