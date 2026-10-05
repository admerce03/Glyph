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
        ImageViewerTooltips.RunOfflineOcrOnThisImage.Should().Contain("select text");
        ImageViewerTooltips.CopySelectedOcrWordsOrAll.Should().Contain("Copy selected OCR");
        ImageViewerTooltips.HighlightOcrWordsMatchingQuery.Should().Contain("matching");
    }
}
