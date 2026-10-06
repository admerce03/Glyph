using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class SessionRestorePolicyTests
{
    [Fact]
    public void Documents_multi_window_tab_restore_without_bounds()
    {
        SessionRestorePolicy.RestoresSingleWindowTabList.Should().BeTrue();
        SessionRestorePolicy.RestoresMultiWindowLayout.Should().BeTrue();
        SessionRestorePolicy.RestoresWindowBounds.Should().BeFalse();
        SessionRestorePolicy.ScopeReason.Should().Contain("Windows");
        SessionRestorePolicy.ScopeReason.Should().Contain("Paths");
        SessionRestorePolicy.ScopeReason.Should().Contain("ActiveIndex");
        SessionRestorePolicy.ScopeReason.Should().Contain("Bounds");
    }
}
