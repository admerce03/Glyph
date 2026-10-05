using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class AnimationFrameNavTests
{
    [Theory]
    [InlineData(0, 1, 3, 1)]
    [InlineData(2, 1, 3, 0)]
    [InlineData(0, -1, 3, 2)]
    [InlineData(1, -1, 3, 0)]
    public void WrapStep_cycles(int current, int delta, int count, int expected)
    {
        AnimationFrameNav.WrapStep(current, delta, count).Should().Be(expected);
    }

    [Fact]
    public void WrapStep_empty_is_minus_one()
    {
        AnimationFrameNav.WrapStep(0, 1, 0).Should().Be(-1);
    }

    [Fact]
    public void FormatLabel_is_one_based()
    {
        AnimationFrameNav.FormatLabel(0, 5).Should().Be("Frame 1/5");
        AnimationFrameNav.FormatLabel(4, 5).Should().Be("Frame 5/5");
    }

    [Fact]
    public void NextPlaybackFrame_advances_within_clip()
    {
        var loops = 0;
        AnimationFrameNav.NextPlaybackFrame(0, 3, loopEnabled: true, 0, ref loops)
            .Should().Be(1);
        loops.Should().Be(0);
    }

    [Fact]
    public void NextPlaybackFrame_stops_when_loop_disabled()
    {
        var loops = 0;
        AnimationFrameNav.NextPlaybackFrame(2, 3, loopEnabled: false, 0, ref loops)
            .Should().BeNull();
    }

    [Fact]
    public void NextPlaybackFrame_infinite_loop_restarts()
    {
        var loops = 0;
        AnimationFrameNav.NextPlaybackFrame(2, 3, loopEnabled: true, animationIterations: 0, ref loops)
            .Should().Be(0);
        loops.Should().Be(0);
    }

    [Fact]
    public void NextPlaybackFrame_finite_loops_then_stops()
    {
        var loops = 0;
        AnimationFrameNav.NextPlaybackFrame(2, 3, loopEnabled: true, animationIterations: 2, ref loops)
            .Should().Be(0);
        loops.Should().Be(1);

        AnimationFrameNav.NextPlaybackFrame(2, 3, loopEnabled: true, animationIterations: 2, ref loops)
            .Should().BeNull();
        loops.Should().Be(2);
    }

    [Fact]
    public void Playback_status_and_save_names()
    {
        AnimationFrameNav.Paused.Should().Contain("paused");
        AnimationFrameNav.Restarted.Should().Contain("restarted");
        AnimationFrameNav.Finished(4).Should().Contain("4/4");
        AnimationFrameNav.SuggestedFileName("photo", 3).Should().Be("photo-frame3.png");
        AnimationFrameNav.PlayButtonLabel(true).Should().Be("Pause");
        AnimationFrameNav.SavedFrame(2, "a.png").Should().Contain("frame 2");
    }
}
