using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class FindAllOpenPdfsStatusTests
{
    [Fact]
    public void Status_strings_cover_flow()
    {
        FindAllOpenPdfsStatus.Searching(3).Should().Contain("3");
        FindAllOpenPdfsStatus.MatchCount(12).Should().StartWith("12");
        FindAllOpenPdfsStatus.OpenedHit("a.pdf", 0).Should().Be("Opened a.pdf p.1.");
        FindAllOpenPdfsStatus.NoDocuments.Should().Contain("PDF");
        FindAllOpenPdfsStatus.EmptyQuery.Should().Contain("search");
        FindAllOpenPdfsStatus.QueryPlaceholder.Should().Contain("Search");
        FindAllOpenPdfsStatus.AnyWordLabel.Should().Be("Any word");
        FindAllOpenPdfsStatus.AnyWordTooltip.Should().Contain("any word");
        FindAllOpenPdfsStatus.MatchCaseLabel.Should().Be("Match case");
        FindAllOpenPdfsStatus.MatchCaseTooltip.Should().Contain("Case-sensitive");
    }
}
