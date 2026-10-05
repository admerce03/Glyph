using FluentAssertions;
using Glyph.Core.Ocr;

namespace Glyph.Core.Tests;

public class OcrLayerPolicyTests
{
    [Fact]
    public void Declares_on_demand_and_preserve_underlay()
    {
        OcrLayerPolicy.DetectOnDemand.Should().BeTrue();
        OcrLayerPolicy.PreserveImageUnderOverlay.Should().BeTrue();
        OcrLayerPolicy.DeclaredBehaviors.Should().Contain(b => b.Contains("Full-bleed"));
    }
}
