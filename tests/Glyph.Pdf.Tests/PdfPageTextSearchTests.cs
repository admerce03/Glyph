using FluentAssertions;
using Glyph.Pdf.Text;
using Xunit;

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
    public void Merge_returns_native_when_ocr_empty()
    {
        var native = new[] { new PdfSearchHit(0, "a", 0, 1) };
        PdfPageTextSearch.Merge(native, []).Should().BeSameAs(native);
    }

    [Fact]
    public void Merge_returns_ocr_when_native_empty()
    {
        var ocr = new[] { new PdfSearchHit(1, "b", 2, 1) };
        PdfPageTextSearch.Merge([], ocr).Should().BeSameAs(ocr);
    }

    [Fact]
    public void Merge_orders_by_page_then_start_and_dedupes()
    {
        var native = new[]
        {
            new PdfSearchHit(1, "later", 10, 3),
            new PdfSearchHit(0, "dup", 5, 3),
        };
        var ocr = new[]
        {
            new PdfSearchHit(0, "ocr-first", 1, 3),
            new PdfSearchHit(0, "dup-ocr", 5, 3),
            new PdfSearchHit(2, "tail", 0, 4),
        };

        var merged = PdfPageTextSearch.Merge(native, ocr);
        merged.Select(h => (h.PageIndex, h.MatchStart, h.MatchLength, h.Snippet))
            .Should()
            .Equal(
                (0, 1, 3, "ocr-first"),
                (0, 5, 3, "dup"),
                (1, 10, 3, "later"),
                (2, 0, 4, "tail"));
    }

    [Fact]
    public void Merge_both_empty_returns_empty()
    {
        PdfPageTextSearch.Merge([], []).Should().BeEmpty();
    }
}
