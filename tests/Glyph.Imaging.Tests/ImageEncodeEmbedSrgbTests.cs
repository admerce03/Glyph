using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageEncodeEmbedSrgbTests
{
    [Fact]
    public void WithEmbedFlag_sets_option()
    {
        var opts = new ImageEncodeOptions(Quality: 90);
        ImageEncodeEmbedSrgb.WithEmbedFlag(opts, true).EmbedSrgbProfile.Should().BeTrue();
        ImageEncodeEmbedSrgb.CheckboxLabel.Should().Contain("sRGB");
    }
}
