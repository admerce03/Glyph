using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageViewerTooltipsTests
{
    [Fact]
    public void Core_tooltips_are_stable()
    {
        ImageViewerTooltips.CropAspectFreeOriginalImageRatio.Should().Be("Crop aspect: free, original image ratio, or common presets");
        ImageViewerTooltips.ClearSelection.Should().Contain("Clear");
    }
}
