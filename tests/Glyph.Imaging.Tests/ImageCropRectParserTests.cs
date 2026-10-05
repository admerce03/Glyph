using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Xunit;

namespace Glyph.Imaging.Tests;

public class ImageCropRectParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1,2,3")]
    [InlineData("a,b,c,d")]
    [InlineData("1,2,0,4")]
    [InlineData("1,2,3,-1")]
    public void TryParse_rejects_invalid(string? text)
    {
        ImageCropRectParser.TryParse(text).Should().BeNull();
    }

    [Fact]
    public void TryParse_valid_rect()
    {
        ImageCropRectParser.TryParse("10, 20, 100, 50")
            .Should().Be(new ImageRect(10, 20, 100, 50));
    }
}
