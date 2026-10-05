using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class BackgroundSearchIndexPolicyTests
{
    [Fact]
    public void Search_stays_on_demand()
    {
        BackgroundSearchIndexPolicy.BackgroundIndexingEnabled.Should().BeFalse();
        BackgroundSearchIndexPolicy.SearchIsOnDemand.Should().BeTrue();
        BackgroundSearchIndexPolicy.DeferredReason.Should().Contain("on demand");
    }
}
