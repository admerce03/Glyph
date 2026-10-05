using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class BatchProgressUiTests
{
    [Fact]
    public void Progress_labels()
    {
        BatchProgressUi.ProgressLabel(0, 5).Should().Be("0 / 5");
        BatchProgressUi.ProgressWithFile(2, 5, "a.png").Should().Contain("a.png");
        BatchProgressUi.CancelledStatus("Batch resize", 3).Should().Contain("3 file");
    }
}

public class ImageRenderingIntentUiTests
{
    [Fact]
    public void Labels_and_status()
    {
        ImageRenderingIntentUi.Labels.Should().HaveCount(4);
        ImageRenderingIntentUi.FromComboIndex(2).Should().Be(ImageRenderingIntent.Saturation);
        ImageRenderingIntentUi.StatusAfterChange(ImageRenderingIntent.Relative)
            .Should().Contain("Relative");
    }
}

public class ImagePanPolicyTests
{
    [Fact]
    public void Declares_scrollviewer_pan()
    {
        ImagePanPolicy.UsesScrollViewer.Should().BeTrue();
        ImagePanPolicy.DeclaredBehaviors.Should().Contain(b => b.Contains("ScrollViewer"));
    }
}
