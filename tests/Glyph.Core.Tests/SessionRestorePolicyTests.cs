using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class SessionRestorePolicyTests
{
    [Fact]
    public void Documents_multi_window_tab_and_bounds_restore()
    {
        SessionRestorePolicy.RestoresSingleWindowTabList.Should().BeTrue();
        SessionRestorePolicy.RestoresMultiWindowLayout.Should().BeTrue();
        SessionRestorePolicy.RestoresWindowBounds.Should().BeTrue();
        SessionRestorePolicy.ScopeReason.Should().Contain("Windows");
        SessionRestorePolicy.ScopeReason.Should().Contain("bounds");
        SessionRestorePolicy.ScopeReason.Should().Contain("Paths");
        SessionRestorePolicy.ScopeReason.Should().Contain("ActiveIndex");
    }
}

public class SessionWindowBoundsPolicyTests
{
    [Fact]
    public void HasUsableBounds_enforces_minimums()
    {
        SessionWindowBoundsPolicy.HasUsableBounds(479, 400).Should().BeFalse();
        SessionWindowBoundsPolicy.HasUsableBounds(480, 319).Should().BeFalse();
        SessionWindowBoundsPolicy.HasUsableBounds(480, 320).Should().BeTrue();
    }

    [Fact]
    public void ClampToWorkAreas_keeps_on_screen_window()
    {
        var areas = new[] { (X: 0, Y: 0, Width: 1920, Height: 1080) };
        var (x, y, w, h) = SessionWindowBoundsPolicy.ClampToWorkAreas(100, 80, 800, 600, areas);
        x.Should().Be(100);
        y.Should().Be(80);
        w.Should().Be(800);
        h.Should().Be(600);
    }

    [Fact]
    public void ClampToWorkAreas_recenters_when_completely_off_screen()
    {
        var areas = new[] { (X: 0, Y: 0, Width: 1920, Height: 1080) };
        var (x, y, w, h) = SessionWindowBoundsPolicy.ClampToWorkAreas(5000, 5000, 800, 600, areas);
        w.Should().Be(800);
        h.Should().Be(600);
        x.Should().BeGreaterThanOrEqualTo(0);
        y.Should().BeGreaterThanOrEqualTo(0);
        (x + w).Should().BeLessThanOrEqualTo(1920);
        (y + h).Should().BeLessThanOrEqualTo(1080);
    }
}
