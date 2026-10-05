using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class BackgroundSearchIndexPolicyTests
{
    [Fact]
    public void Background_indexing_is_enabled()
    {
        BackgroundSearchIndexPolicy.BackgroundIndexingEnabled.Should().BeTrue();
        BackgroundSearchIndexPolicy.SearchIsOnDemand.Should().BeTrue();
        BackgroundSearchIndexPolicy.Reason.Should().Contain("WarmIndexAsync");
    }
}
