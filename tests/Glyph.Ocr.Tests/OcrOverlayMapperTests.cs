using FluentAssertions;
using Glyph.Ocr.Abstractions;
using Xunit;

namespace Glyph.Ocr.Tests;

public class OcrOverlayMapperTests
{
    [Fact]
    public void MapToDisplay_scales_word_box_to_display_bitmap()
    {
        var word = new OcrWord("hi", X: 10, Y: 20, Width: 40, Height: 10);
        var mapped = OcrOverlayMapper.MapToDisplay(word, sourceWidth: 100, sourceHeight: 50, displayWidth: 200, displayHeight: 100);
        mapped.X.Should().Be(20);
        mapped.Y.Should().Be(40);
        mapped.Width.Should().Be(80);
        mapped.Height.Should().Be(20);
    }

    [Fact]
    public void MapToDisplay_enforces_minimum_one_pixel_size()
    {
        var word = new OcrWord(".", X: 0, Y: 0, Width: 0.1, Height: 0.1);
        var mapped = OcrOverlayMapper.MapToDisplay(word, 1000, 1000, 100, 100);
        mapped.Width.Should().Be(1);
        mapped.Height.Should().Be(1);
    }
}
