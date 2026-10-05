using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageAdjustLivePreviewPolicyTests
{
    [Fact]
    public void Checkpoint_preview_contract()
    {
        ImageAdjustLivePreviewPolicy.UsesCheckpointPreview.Should().BeTrue();
        ImageAdjustLivePreviewPolicy.ShouldRestoreBaselineOnCancel(true).Should().BeTrue();
        ImageAdjustLivePreviewPolicy.ShouldRestoreBaselineOnCancel(false).Should().BeFalse();
    }
}
