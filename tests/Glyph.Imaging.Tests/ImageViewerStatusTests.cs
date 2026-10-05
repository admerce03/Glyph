using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Xunit;

namespace Glyph.Imaging.Tests;

public class ImageViewerStatusTests
{
    [Fact]
    public void Mode_selection_and_batch_labels()
    {
        ImageViewerStatus.DrawModeOff.Should().Contain("off");
        ImageSelectionClipboardPolicy.NeedSelection.Should().Contain("selection");
        ImageViewerStatus.FormatCopiedImage(10, 20).Should().Contain("10");
        ImageViewerStatus.FormatLassoSelected(3, 4, 5).Should().Contain("5 pts");
        ImageViewerStatus.FormatMarkupTool("Ink").Should().Contain("Ink");
        BatchProgressUi.FormatConvertWrote("PNG", 2).Should().Contain("PNG");
        BatchProgressUi.FormatRenamed(3).Should().Contain("3");
        ImageViewerStatus.FormatSelectionRestored("rect", 1, 2).Should().Contain("rect");
        ImageViewerStatus.FormatPrintUiShown(2, 2).Should().Contain("2-up");
        ImageViewerStatus.RotatedLeft.Should().Be("Rotated left.");
        ImageViewerStatus.FlippedHorizontally.Should().Contain("horizontally");
        ImageViewerStatus.AssignedSrgbIcc.Should().Contain("sRGB");
    }
}

