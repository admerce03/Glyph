using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageSelectionClipboardPolicyTests
{
    [Fact]
    public void Copy_cut_status_strings()
    {
        ImageSelectionClipboardPolicy.Copied(false, 10, 20).Should().Be("Copied selection 10×20.");
        ImageSelectionClipboardPolicy.Copied(true, 10, 20).Should().Contain("inverted");
        ImageSelectionClipboardPolicy.Cut(false, 5, 5).Should().Be("Cut selection 5×5.");
        ImageSelectionClipboardPolicy.Cut(true, 5, 5).Should().Contain("hole");
        ImageSelectionClipboardPolicy.NeedSelection.Should().Contain("selection");
    }
}
