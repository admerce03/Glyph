using FluentAssertions;
using Glyph.Pdf.Text;

namespace Glyph.Pdf.Tests;

public class PdfPageTextSearchTests
{
    [Fact]
    public void Find_returns_hits_across_pages_with_snippets()
    {
        var pages = new Dictionary<int, string>
        {
            [0] = "Hello world from page one.",
            [2] = "Another world on page three.",
        };

        var hits = PdfPageTextSearch.Find(pages, "world");

        hits.Should().HaveCount(2);
        hits[0].PageIndex.Should().Be(0);
        hits[0].Snippet.Should().Contain("world");
        hits[1].PageIndex.Should().Be(2);
        hits[1].MatchLength.Should().Be(5);
    }

    [Fact]
    public void Find_respects_case_sensitivity()
    {
        var pages = new Dictionary<int, string> { [0] = "World WORLD world" };
        PdfPageTextSearch.Find(pages, "WORLD", caseSensitive: true).Should().ContainSingle();
        PdfPageTextSearch.Find(pages, "WORLD", caseSensitive: false).Should().HaveCount(3);
    }

    [Fact]
    public void Find_returns_empty_for_blank_query_or_texts()
    {
        PdfPageTextSearch.Find(new Dictionary<int, string> { [0] = "abc" }, "  ").Should().BeEmpty();
        PdfPageTextSearch.Find(new Dictionary<int, string>(), "abc").Should().BeEmpty();
    }

    [Fact]
    public void Merge_orders_and_dedupes_native_and_ocr_hits()
    {
        var native = new[]
        {
            new PdfSearchHit(1, "b", 2, 1),
            new PdfSearchHit(0, "a", 0, 1),
        };
        var ocr = new[]
        {
            new PdfSearchHit(0, "a", 0, 1), // duplicate of native
            new PdfSearchHit(0, "c", 5, 1),
        };

        var merged = PdfPageTextSearch.Merge(native, ocr);

        merged.Should().HaveCount(3);
        merged.Select(h => (h.PageIndex, h.MatchStart)).Should().ContainInOrder(
            (0, 0),
            (0, 5),
            (1, 2));
    }
}
