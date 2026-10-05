using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageCropSelectionPolicyTests
{
    [Fact]
    public void Crop_status_strings()
    {
        ImageCropSelectionPolicy.CroppedToSelection(10, 20).Should().Contain("selection 10×20");
        ImageCropSelectionPolicy.CroppedTo(10, 20).Should().Be("Cropped to 10×20.");
        ImageCropSelectionPolicy.CannotCropInverted.Should().Contain("inverted");
        ImageCropSelectionPolicy.NeedRegion.Should().Contain("crop region");
    }
}
