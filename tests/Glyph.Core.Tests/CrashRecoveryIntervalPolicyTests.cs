using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class CrashRecoveryIntervalPolicyTests
{
    [Fact]
    public void IsEnabled_false_when_zero_or_negative()
    {
        CrashRecoveryIntervalPolicy.IsEnabled(0).Should().BeFalse();
        CrashRecoveryIntervalPolicy.IsEnabled(-1).Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_true_when_positive()
    {
        CrashRecoveryIntervalPolicy.IsEnabled(15).Should().BeTrue();
        CrashRecoveryIntervalPolicy.IsEnabled(120).Should().BeTrue();
    }

    [Fact]
    public void ClampActiveSeconds_clamps_to_active_range()
    {
        CrashRecoveryIntervalPolicy.ClampActiveSeconds(1).Should().Be(15);
        CrashRecoveryIntervalPolicy.ClampActiveSeconds(120).Should().Be(120);
        CrashRecoveryIntervalPolicy.ClampActiveSeconds(9999).Should().Be(3600);
    }
}
