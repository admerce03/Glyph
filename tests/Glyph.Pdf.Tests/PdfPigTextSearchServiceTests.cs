using FluentAssertions;
using Glyph.Pdf.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfPigTextSearchServiceTests
{
    private readonly PdfPigTextSearchService _search = new();

    [Fact]
    public async Task Search_finds_match_on_one_page()
    {
        await using var pdf = await SamplePdf.CreateAsync(b =>
        {
            b.Page("Only page has alpha once");
        });

        var result = await _search.SearchAsync(pdf.Path, "alpha");
        result.Status.Should().Be(PdfSearchStatus.Success);
        result.Hits.Should().ContainSingle();
        result.Hits[0].PageIndex.Should().Be(0);
    }

    [Fact]
    public async Task Search_finds_matches_across_multiple_pages()
    {
        await using var pdf = await SamplePdf.CreateAsync(b =>
        {
            b.Page("Hello alpha world");
            b.Page("Second page with bravo");
        });

        var alpha = await _search.SearchAsync(pdf.Path, "alpha");
        alpha.Hits.Should().ContainSingle();
        alpha.Hits[0].PageIndex.Should().Be(0);

        var bravo = await _search.SearchAsync(pdf.Path, "bravo");
        bravo.Hits.Should().ContainSingle();
        bravo.Hits[0].PageIndex.Should().Be(1);
    }

    [Fact]
    public async Task Search_finds_multiple_matches_on_same_page()
    {
        await using var pdf = await SamplePdf.CreateAsync(b =>
        {
            b.Page("alpha one and alpha two and alpha three");
        });

        var result = await _search.SearchAsync(pdf.Path, "alpha");
        result.Hits.Should().HaveCount(3);
        result.Hits.Select(h => h.PageIndex).Should().OnlyContain(i => i == 0);
    }

    [Fact]
    public async Task Search_returns_no_matches_for_absent_term()
    {
        await using var pdf = await SamplePdf.CreateAsync(b => b.Page("Hello alpha world"));
        var result = await _search.SearchAsync(pdf.Path, "zzz-not-present");
        result.Status.Should().Be(PdfSearchStatus.NoMatches);
        result.Hits.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_empty_query_returns_empty_query_status()
    {
        await using var pdf = await SamplePdf.CreateAsync(b => b.Page("Hello alpha world"));
        var result = await _search.SearchAsync(pdf.Path, "   ");
        result.Status.Should().Be(PdfSearchStatus.EmptyQuery);
        result.Hits.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_is_case_insensitive_by_default()
    {
        await using var pdf = await SamplePdf.CreateAsync(b => b.Page("Hello alpha world"));
        var result = await _search.SearchAsync(pdf.Path, "ALPHA");
        result.Hits.Should().ContainSingle();
    }

    [Fact]
    public async Task Search_respects_case_sensitive_option()
    {
        await using var pdf = await SamplePdf.CreateAsync(b => b.Page("Hello alpha world"));
        var sensitive = await _search.SearchAsync(
            pdf.Path,
            "ALPHA",
            new PdfSearchOptions(CaseSensitive: true));
        sensitive.Status.Should().Be(PdfSearchStatus.NoMatches);

        var exactCase = await _search.SearchAsync(
            pdf.Path,
            "alpha",
            new PdfSearchOptions(CaseSensitive: true));
        exactCase.Hits.Should().ContainSingle();
    }

    [Fact]
    public async Task Search_supports_exact_phrase()
    {
        await using var pdf = await SamplePdf.CreateAsync(b =>
        {
            b.Page("the quick brown fox");
        });

        var phrase = await _search.SearchAsync(
            pdf.Path,
            "quick brown",
            new PdfSearchOptions(ExactPhrase: true));
        phrase.Hits.Should().ContainSingle();

        var missingPhrase = await _search.SearchAsync(
            pdf.Path,
            "quick fox",
            new PdfSearchOptions(ExactPhrase: true));
        missingPhrase.Status.Should().Be(PdfSearchStatus.NoMatches);
    }

    [Fact]
    public async Task Search_handles_punctuation_in_query_and_document()
    {
        await using var pdf = await SamplePdf.CreateAsync(b =>
        {
            b.Page("Punctuation: hello, world! (alpha)");
        });

        var result = await _search.SearchAsync(pdf.Path, "hello, world!");
        result.Hits.Should().ContainSingle();
        result.Hits[0].Snippet.Should().ContainEquivalentOf("hello");
    }

    [Fact]
    public async Task Search_handles_unicode_non_ascii_text()
    {
        var path = FixturePath("unicode-latin.pdf");
        File.Exists(path).Should().BeTrue("unicode fixture must ship with tests");

        var result = await _search.SearchAsync(path, "café");
        result.Status.Should().Be(PdfSearchStatus.Success);
        result.Hits.Should().ContainSingle();
        result.Hits[0].Snippet.Should().Contain("café");
    }

    [Fact]
    public async Task Search_tolerates_imperfect_extraction_missing_spaces()
    {
        // Two separate text draws often extract as "helloworld" with no space.
        await using var pdf = await SamplePdf.CreateAsync(b =>
        {
            b.PageLines("hello", "world");
        });

        var result = await _search.SearchAsync(pdf.Path, "hello world");
        result.Hits.Should().ContainSingle("whitespace-insensitive fallback should match");
    }

    [Fact]
    public async Task Search_image_only_pdf_indicates_ocr_required()
    {
        await using var pdf = await SamplePdf.CreateImageOnlyAsync();
        var result = await _search.SearchAsync(pdf.Path, "anything");
        result.Status.Should().Be(PdfSearchStatus.NoExtractableText);
        result.Message.Should().ContainEquivalentOf("OCR");
        result.Hits.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_encrypted_pdf_without_password_fails_gracefully()
    {
        var path = FixturePath("encrypted.pdf");
        File.Exists(path).Should().BeTrue();

        var result = await _search.SearchAsync(path, "alpha");
        result.Status.Should().Be(PdfSearchStatus.DocumentEncrypted);
        result.Hits.Should().BeEmpty();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Search_respects_cancellation()
    {
        await using var pdf = await SamplePdf.CreateAsync(b =>
        {
            for (var i = 0; i < 80; i++)
            {
                b.Page($"page {i} filler token-{i}");
            }

            b.Page("unique-needle-end");
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _search.SearchAsync(pdf.Path, "unique-needle-end", cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Search_hit_page_indexes_support_navigation_to_each_result()
    {
        await using var pdf = await SamplePdf.CreateAsync(b =>
        {
            b.Page("alpha on first");
            b.Page("nope");
            b.Page("alpha on third");
        });

        var result = await _search.SearchAsync(pdf.Path, "alpha");
        result.Hits.Select(h => h.PageIndex).Should().Equal(0, 2);
    }

    [Fact]
    public async Task Search_large_pdf_completes_without_blocking_failure()
    {
        await using var pdf = await SamplePdf.CreateAsync(b =>
        {
            for (var i = 0; i < 120; i++)
            {
                b.Page($"Page {i + 1} marker token-{i} filler text for search stress");
            }

            b.Page("unique-needle-end");
        });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _search.SearchAsync(pdf.Path, "unique-needle-end");
        sw.Stop();

        result.Hits.Should().ContainSingle();
        result.Hits[0].PageIndex.Should().Be(120);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void NormalizeForSearch_collapses_whitespace()
    {
        PdfPigTextSearchService.NormalizeForSearch("hello \n\t world").Should().Be("hello world");
    }

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
}

public sealed class PdfSearchCoordinatorTests
{
    [Fact]
    public async Task Starting_new_search_cancels_in_progress_search()
    {
        var slow = new SlowSearchService(delayPerCall: TimeSpan.FromMilliseconds(400));
        using var coordinator = new PdfSearchCoordinator(slow);

        var first = coordinator.SearchAsync("doc.pdf", "one");
        // Give the first search a moment to start.
        await Task.Delay(50);
        var second = await coordinator.SearchAsync("doc.pdf", "two");

        second.Status.Should().Be(PdfSearchStatus.Success);
        second.Hits.Should().ContainSingle(h => h.Snippet == "two");

        var firstResult = await first;
        firstResult.Status.Should().Be(PdfSearchStatus.Cancelled);
    }

    [Fact]
    public async Task Repeated_searches_do_not_return_stale_hits()
    {
        var service = new ScriptedSearchService();
        using var coordinator = new PdfSearchCoordinator(service);

        var a = await coordinator.SearchAsync("doc.pdf", "alpha");
        a.Hits.Should().ContainSingle(h => h.Snippet == "alpha");

        var b = await coordinator.SearchAsync("doc.pdf", "bravo");
        b.Hits.Should().ContainSingle(h => h.Snippet == "bravo");
        b.Hits.Should().NotContain(h => h.Snippet == "alpha");
    }

    private sealed class SlowSearchService : IPdfTextSearchService
    {
        private readonly TimeSpan _delayPerCall;

        public SlowSearchService(TimeSpan delayPerCall) => _delayPerCall = delayPerCall;

        public async Task<PdfSearchResult> SearchAsync(
            string path,
            string query,
            PdfSearchOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(_delayPerCall, cancellationToken);
            return PdfSearchResult.Success(
            [
                new PdfSearchHit(0, query, 0, query.Length),
            ]);
        }
    }

    private sealed class ScriptedSearchService : IPdfTextSearchService
    {
        public Task<PdfSearchResult> SearchAsync(
            string path,
            string query,
            PdfSearchOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PdfSearchResult.Success(
            [
                new PdfSearchHit(0, query, 0, query.Length),
            ]));
        }
    }
}

internal sealed class SamplePdf : IAsyncDisposable
{
    private SamplePdf(string path) => Path = path;

    public string Path { get; }

    public static Task<SamplePdf> CreateAsync(Action<SamplePdfBuilder> build)
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "glyph-search-" + Guid.NewGuid().ToString("N") + ".pdf");
        var builder = new SamplePdfBuilder();
        build(builder);
        File.WriteAllBytes(path, builder.Build());
        return Task.FromResult(new SamplePdf(path));
    }

    public static Task<SamplePdf> CreateImageOnlyAsync()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "glyph-image-only-" + Guid.NewGuid().ToString("N") + ".pdf");

        // Minimal valid 1x1 JPEG.
        var jpeg = Convert.FromBase64String(
            "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAn/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/8QAFQEBAQAAAAAAAAAAAAAAAAAAAAX/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIQAxAAAAGfAP/EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAQUCf//EABQRAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQMBAT8Bf//EABQRAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQIBAT8Bf//Z");

        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.Letter);
        page.AddJpeg(jpeg, new PdfRectangle(50, 600, 150, 700));
        File.WriteAllBytes(path, builder.Build());
        return Task.FromResult(new SamplePdf(path));
    }

    public ValueTask DisposeAsync()
    {
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class SamplePdfBuilder
{
    private readonly PdfDocumentBuilder _builder = new();
    private readonly PdfDocumentBuilder.AddedFont _font;

    public SamplePdfBuilder()
    {
        _font = _builder.AddStandard14Font(Standard14Font.Helvetica);
    }

    public void Page(string text)
    {
        var page = _builder.AddPage(PageSize.Letter);
        page.AddText(text, 14, new PdfPoint(50, 700), _font);
    }

    public void PageLines(params string[] lines)
    {
        var page = _builder.AddPage(PageSize.Letter);
        var y = 700.0;
        foreach (var line in lines)
        {
            page.AddText(line, 14, new PdfPoint(50, y), _font);
            y -= 20;
        }
    }

    public byte[] Build() => _builder.Build();
}
