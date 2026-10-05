using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageAdvancedDeferredPolicyTests
{
    [Fact]
    public void Hdr_heif_ml_map_deferred()
    {
        ImageAdvancedDeferredPolicy.HdrDisplayPipelineSupported.Should().BeFalse();
        ImageAdvancedDeferredPolicy.HeifEncodeDelegateAvailable.Should().BeFalse();
        ImageAdvancedDeferredPolicy.AutomaticSubjectDetectionSupported.Should().BeFalse();
        ImageAdvancedDeferredPolicy.EmbeddedMapWebViewSupported.Should().BeFalse();
        ImageAdvancedDeferredPolicy.HeifEncodeReason.Should().Contain("HEIF");
    }
}
