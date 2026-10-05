using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PresentationModeDefaultsTests
{
    [Fact]
    public void AutoAdvance_is_eight_seconds()
    {
        PresentationModeDefaults.AutoAdvanceInterval.Should().Be(TimeSpan.FromSeconds(8));
    }

    [Fact]
    public void StatusMessage_mentions_navigation_and_escape()
    {
        PresentationModeDefaults.StatusMessage.Should().Contain("←/→");
        PresentationModeDefaults.StatusMessage.Should().Contain("Esc");
        PresentationModeDefaults.StatusMessage.Should().Contain("8s");
    }
}
