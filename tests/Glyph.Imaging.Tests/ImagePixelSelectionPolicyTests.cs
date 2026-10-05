using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImagePixelSelectionPolicyTests
{
    [Fact]
    public void Full_image_rect_and_status()
    {
        ImagePixelSelectionPolicy.FullImageRect(800, 600).Should().Be(new ImageRect(0, 0, 800, 600));
        ImagePixelSelectionPolicy.SelectedAll(800, 600).Should().Contain("800×600");
        ImagePixelSelectionPolicy.Cleared.Should().Contain("cleared");
        ImagePixelSelectionPolicy.ModeOff.Should().Contain("off");
    }
}
