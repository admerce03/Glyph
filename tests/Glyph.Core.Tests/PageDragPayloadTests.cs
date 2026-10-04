using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PageDragPayloadTests
{
    [Fact]
    public void Format_and_parse_round_trip()
    {
        var payload = new PageDragPayload("doc-abc", [2, 0, 2, 5]);
        var text = payload.Format();

        PageDragPayload.TryParse(text, out var parsed).Should().BeTrue();
        parsed!.DocumentKey.Should().Be("doc-abc");
        parsed.PageIndexes.Should().Equal(0, 2, 5);
    }

    [Fact]
    public void TryParse_accepts_legacy_reorder_prefix()
    {
        PageDragPayload.TryParse("glyph-page-reorder:1,3", out var parsed).Should().BeTrue();
        parsed!.DocumentKey.Should().BeEmpty();
        parsed.PageIndexes.Should().Equal(1, 3);
    }

    [Fact]
    public void TryParse_rejects_garbage()
    {
        PageDragPayload.TryParse("not-a-payload", out _).Should().BeFalse();
        PageDragPayload.TryParse("glyph-pages:v1:nokey", out _).Should().BeFalse();
        PageDragPayload.TryParse("glyph-pages:v1:key|", out _).Should().BeFalse();
        PageDragPayload.TryParse("glyph-pages:v1:key|-1", out _).Should().BeFalse();
    }
}
