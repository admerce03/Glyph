using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class MonitorCyclePolicyTests
{
    [Theory]
    [InlineData(0, 1, -1)]
    [InlineData(0, 2, 1)]
    [InlineData(1, 2, 0)]
    [InlineData(2, 3, 0)]
    [InlineData(-1, 3, 0)]
    [InlineData(5, 3, 0)]
    public void NextIndex_rings(int current, int count, int expected)
    {
        MonitorCyclePolicy.NextIndex(current, count).Should().Be(expected);
    }

    [Fact]
    public void SingleMonitorStatus_is_set()
    {
        MonitorCyclePolicy.SingleMonitorStatus.Should().Contain("one monitor");
    }

    [Fact]
    public void FitInWorkArea_centers_and_clamps()
    {
        var (x, y, w, h) = MonitorCyclePolicy.FitInWorkArea(100, 200, 800, 600, 400, 300);
        x.Should().Be(300);
        y.Should().Be(350);
        w.Should().Be(400);
        h.Should().Be(300);

        var oversized = MonitorCyclePolicy.FitInWorkArea(0, 0, 100, 80, 400, 300);
        oversized.Width.Should().Be(100);
        oversized.Height.Should().Be(80);
        oversized.X.Should().Be(0);
        oversized.Y.Should().Be(0);
    }

    [Fact]
    public void MovedStatus_is_1_based()
    {
        MonitorCyclePolicy.MovedStatus(0, 2).Should().Be("Moved window to monitor 1 of 2.");
        MonitorCyclePolicy.MovedStatus(1, 2).Should().Be("Moved window to monitor 2 of 2.");
    }
}
