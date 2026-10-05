using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageCropMapperTests
{
    [Fact]
    public void ToDocumentPixels_maps_1_to_1_display()
    {
        var rect = ImageCropMapper.ToDocumentPixels(10, 20, 40, 30, displayWidth: 100, displayHeight: 80, documentWidth: 100, documentHeight: 80);
        rect.Should().Be(new ImageRect(10, 20, 40, 30));
    }

    [Fact]
    public void ToDocumentPixels_scales_downscaled_display_to_document()
    {
        // Display is half of document.
        var rect = ImageCropMapper.ToDocumentPixels(5, 10, 20, 15, displayWidth: 50, displayHeight: 40, documentWidth: 100, documentHeight: 80);
        rect.Should().Be(new ImageRect(10, 20, 40, 30));
    }

    [Fact]
    public void ToDocumentPixels_clamps_to_document_bounds()
    {
        var rect = ImageCropMapper.ToDocumentPixels(-10, -5, 200, 200, displayWidth: 100, displayHeight: 100, documentWidth: 100, documentHeight: 100);
        rect.X.Should().Be(0);
        rect.Y.Should().Be(0);
        rect.Width.Should().Be(100);
        rect.Height.Should().Be(100);
    }
}
