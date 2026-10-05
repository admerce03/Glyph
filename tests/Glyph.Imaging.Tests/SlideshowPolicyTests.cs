using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class SlideshowPolicyTests
{
    [Fact]
    public void Interval_and_start_gate()
    {
        SlideshowPolicy.Interval.TotalSeconds.Should().Be(3);
        SlideshowPolicy.CanStart(1).Should().BeFalse();
        SlideshowPolicy.CanStart(2).Should().BeTrue();
        SlideshowPolicy.ButtonLabel(false).Should().Be("Slideshow");
        SlideshowPolicy.ButtonLabel(true).Should().Be("Stop show");
        SlideshowPolicy.Started.Should().Contain("3s");
    }
}
