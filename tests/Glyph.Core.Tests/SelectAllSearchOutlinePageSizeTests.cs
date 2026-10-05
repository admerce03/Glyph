using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.Pdf;
using Glyph.Core.Text;

namespace Glyph.Core.Tests;

public class PdfTextSelectAllPolicyTests
{
    [Fact]
    public void ShouldExpandToDocument_on_second_full_page_select()
    {
        PdfTextSelectAllPolicy.ShouldExpandToDocument(0, 0, "hello", "hello", 3).Should().BeTrue();
        PdfTextSelectAllPolicy.ShouldExpandToDocument(0, 0, "hello", "hello", 1).Should().BeFalse();
        PdfTextSelectAllPolicy.ShouldExpandToDocument(0, 1, "hello", "hello", 3).Should().BeFalse();
        PdfTextSelectAllPolicy.ShouldExpandToDocument(0, 0, "partial", "hello", 3).Should().BeFalse();
    }

    [Fact]
    public void Join_and_status_helpers()
    {
        PdfTextSelectAllPolicy.JoinDocumentParts(["a", " ", "b"]).Should().Be("a\n\nb");
        PdfTextSelectAllPolicy.DocumentStatus(2, 10).Should().Contain("2 page");
        PdfTextSelectAllPolicy.PageStatus(0, 5).Should().Contain("page 1");
    }
}

public class PdfSearchHitOrderTests
{
    [Fact]
    public void ByPageThenOccurrence_sorts_stable()
    {
        var hits = new[] { (Page: 2, Occ: 0), (Page: 0, Occ: 1), (Page: 0, Occ: 0), (Page: 1, Occ: 0) };
        var ordered = PdfSearchHitOrder.ByPageThenOccurrence(hits, h => h.Page, h => h.Occ);
        ordered.Select(h => (h.Page, h.Occ))
            .Should().Equal((0, 0), (0, 1), (1, 0), (2, 0));
    }
}

public class OutlineExpandPolicyTests
{
    [Fact]
    public void Default_is_expanded_and_toggle_flips()
    {
        OutlineExpandPolicy.DefaultIsExpanded.Should().BeTrue();
        OutlineExpandPolicy.ToggleExpanded(true).Should().BeFalse();
        OutlineExpandPolicy.ToggleExpanded(false).Should().BeTrue();
    }
}

public class PdfPageSizeSetTests
{
    [Fact]
    public void HasMixedSizes_detects_variance()
    {
        PdfPageSizeSet.HasMixedSizes(
        [
            new PdfPageSizeSet.Size(612, 792),
            new PdfPageSizeSet.Size(612, 792),
        ]).Should().BeFalse();

        PdfPageSizeSet.HasMixedSizes(
        [
            new PdfPageSizeSet.Size(612, 792),
            new PdfPageSizeSet.Size(792, 612),
        ]).Should().BeTrue();
    }
}
