using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageViewerTextLabelsTests
{
    [Fact]
    public void Labels_are_stable()
    {
        ImageViewerTextLabels.Images.Should().Be("Images");
    }
}
