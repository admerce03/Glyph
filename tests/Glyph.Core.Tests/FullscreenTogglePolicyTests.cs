using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class FullscreenTogglePolicyTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldExitFullscreen(bool current, bool expected)
    {
        FullscreenTogglePolicy.ShouldExitFullscreen(current).Should().Be(expected);
    }

    [Fact]
    public void StatusAfterToggle_mentions_exit_or_enter()
    {
        FullscreenTogglePolicy.StatusAfterToggle(true).Should().Contain("Exited");
        FullscreenTogglePolicy.StatusAfterToggle(false).Should().Contain("Fullscreen");
    }
}
