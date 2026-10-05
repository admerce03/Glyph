using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageViewerChromeLabelsTests
{
    [Fact]
    public void Labels_are_stable()
    {
        ImageViewerChromeLabels.Minus.Should().NotBeNullOrEmpty();
        ImageViewerChromeLabels.Plus.Should().NotBeNullOrEmpty();
        ImageViewerChromeLabels.Fit.Should().NotBeNullOrEmpty();
        ImageViewerChromeLabels.Minus.Should().Be("−");
        ImageViewerChromeLabels.CopyOcr.Should().Be("Copy OCR");
        ImageViewerChromeLabels.Entities.Should().Be("Entities");
        ImageViewerChromeLabels.FindOcr.Should().Be("Find OCR");
        ImageViewerChromeLabels.OcrToPdf.Should().Contain("PDF");
    }
}
