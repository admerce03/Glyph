using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class IntermediateScrollThrottleTests
{
    [Fact]
    public void ShouldRender_false_inside_interval()
    {
        IntermediateScrollThrottle.ShouldRender(100, 50, minIntervalMs: 72).Should().BeFalse();
        IntermediateScrollThrottle.ShouldRender(121, 50, minIntervalMs: 72).Should().BeFalse();
    }

    [Fact]
    public void ShouldRender_true_at_or_after_interval()
    {
        IntermediateScrollThrottle.ShouldRender(122, 50, minIntervalMs: 72).Should().BeTrue();
        IntermediateScrollThrottle.ShouldRender(200, 50).Should().BeTrue();
    }

    [Fact]
    public void Default_interval_is_72ms()
    {
        IntermediateScrollThrottle.MinIntervalMs.Should().Be(72);
    }
}
