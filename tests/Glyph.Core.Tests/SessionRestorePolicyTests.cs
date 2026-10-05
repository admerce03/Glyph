using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class SessionRestorePolicyTests
{
    [Fact]
    public void Documents_single_window_tab_restore_only()
    {
        SessionRestorePolicy.RestoresSingleWindowTabList.Should().BeTrue();
        SessionRestorePolicy.RestoresMultiWindowLayout.Should().BeFalse();
        SessionRestorePolicy.ScopeReason.Should().Contain("Paths");
        SessionRestorePolicy.ScopeReason.Should().Contain("ActiveIndex");
        SessionRestorePolicy.ScopeReason.Should().NotContain("window layout");
    }
}
