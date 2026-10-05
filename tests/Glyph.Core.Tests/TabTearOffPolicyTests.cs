using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class TabTearOffPolicyTests
{
    [Fact]
    public void CanTearOff_requires_existing_path()
    {
        TabTearOffPolicy.CanTearOff(null).Should().BeFalse();
        TabTearOffPolicy.CanTearOff("").Should().BeFalse();
        var missing = Path.Combine(Path.GetTempPath(), "no-" + Guid.NewGuid().ToString("N") + ".pdf");
        TabTearOffPolicy.CanTearOff(missing).Should().BeFalse();

        var path = Path.Combine(Path.GetTempPath(), "tear-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            File.WriteAllText(path, "%PDF");
            TabTearOffPolicy.CanTearOff(path).Should().BeTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void RequiresDirtyResolution(bool dirty, bool unsaved, bool expected)
    {
        TabTearOffPolicy.RequiresDirtyResolution(dirty, unsaved).Should().Be(expected);
    }

    [Fact]
    public void UnsavedChangesPrompt_includes_display_name()
    {
        TabTearOffPolicy.UnsavedChangesPrompt("report.pdf").Should().Contain("report.pdf");
    }
}
