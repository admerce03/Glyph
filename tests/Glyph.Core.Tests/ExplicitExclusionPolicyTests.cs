using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class ExplicitExclusionPolicyTests
{
    [Fact]
    public void Documents_intentional_non_goals()
    {
        ExplicitExclusionPolicy.TouchscreenGesturesSupported.Should().BeFalse();
        ExplicitExclusionPolicy.StylusInputSupported.Should().BeFalse();
        ExplicitExclusionPolicy.WindowsInkSupported.Should().BeFalse();
        ExplicitExclusionPolicy.ExcludedCapabilities.Should().Contain(c => c.Contains("AirDrop"));
        ExplicitExclusionPolicy.ExcludedCapabilities.Should().HaveCountGreaterThan(5);
    }
}
