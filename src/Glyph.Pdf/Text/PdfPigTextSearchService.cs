using System.Collections.Concurrent;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

namespace Glyph.Pdf.Text;

/// <summary>
/// Offline full-text search over PDF text layers using PdfPig.
/// Runs on a thread-pool thread and honors cancellation between pages.
/// Does not require rasterizing pages. Glyph.App must depend only on
/// <see cref="IPdfTextSearchService"/>, never on PdfPig types directly.
/// Optionally warms a per-path page-text index for faster subsequent Finds.
/// </summary>
public sealed class PdfPigTextSearchService : IPdfTextSearchService
{
    private readonly ConcurrentDictionary<string, PdfPageTextIndex> _indexes =
        new(StringComparer.OrdinalIgnoreCase);

    public Task WarmIndexAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (_indexes.TryGetValue(path, out var existing) && existing.IsComplete)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() => WarmCore(path, cancellationToken), cancellationToken);
    }

    public Task<PdfSearchResult> SearchAsync(
        string path,
        string query,
        PdfSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        options ??= new PdfSearchOptions();

        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(PdfSearchResult.EmptyQuery());
        }

        if (_indexes.TryGetValue(path, out var index) && index.IsComplete)
        {
            return Task.FromResult(SearchFromIndex(index, query, options));
        }

        return Task.Run(
            () => SearchCore(path, query, options, cancellationToken),
            cancellationToken);
    }

    private void WarmCore(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var index = _indexes.GetOrAdd(path, _ => new PdfPageTextIndex());
        if (index.IsComplete)
        {
            return;
        }

        try
        {
            using var document = PdfDocument.Open(path);
            index.SetPageCount(document.NumberOfPages);
            for (var pageIndex = 0; pageIndex < document.NumberOfPages; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = document.GetPage(pageIndex + 1);
                index.SetPage(pageIndex, page.Text ?? string.Empty);
            }

            index.MarkComplete();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Leave incomplete index; SearchAsync will fall back to live scan.
            _indexes.TryRemove(path, out _);
        }
    }

    private static PdfSearchResult SearchFromIndex(
        PdfPageTextIndex index,
        string query,
        PdfSearchOptions options)
    {
        var comparison = options.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var needle = NormalizeForSearch(query);
        var hits = new List<PdfSearchHit>();
        var sawAnyText = false;

        foreach (var (pageIndex, raw) in index.Snapshot())
        {
            if (raw.Length > 0)
            {
                sawAnyText = true;
            }

            var haystack = NormalizeForSearch(raw);
            if (haystack.Length == 0 || needle.Length == 0)
            {
                continue;
            }

            if (options.ExactPhrase)
            {
                CollectPhraseHits(hits, pageIndex, haystack, raw, needle, comparison);
            }
            else
            {
                foreach (var token in needle.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    CollectPhraseHits(hits, pageIndex, haystack, raw, token, comparison);
                }
            }
        }

        if (hits.Count > 0)
        {
            return PdfSearchResult.Success(hits);
        }

        if (!sawAnyText && index.PageCount > 0)
        {
            return PdfSearchResult.NoExtractableText();
        }

        return PdfSearchResult.NoMatches();
    }

    private static PdfSearchResult SearchCore(
        string path,
        string query,
        PdfSearchOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var document = PdfDocument.Open(path);
            var comparison = options.CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            var needle = NormalizeForSearch(query);
            var hits = new List<PdfSearchHit>();
            var sawAnyText = false;

            for (var pageIndex = 0; pageIndex < document.NumberOfPages; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = document.GetPage(pageIndex + 1);
                var raw = page.Text ?? string.Empty;
                if (raw.Length > 0)
                {
                    sawAnyText = true;
                }

                var haystack = NormalizeForSearch(raw);
                if (haystack.Length == 0 || needle.Length == 0)
                {
                    continue;
                }

                if (options.ExactPhrase)
                {
                    CollectPhraseHits(hits, pageIndex, haystack, raw, needle, comparison);
                }
                else
                {
                    foreach (var token in needle.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        CollectPhraseHits(hits, pageIndex, haystack, raw, token, comparison);
                    }
                }
            }

            if (hits.Count > 0)
            {
                return PdfSearchResult.Success(hits);
            }

            if (!sawAnyText && document.NumberOfPages > 0)
            {
                return PdfSearchResult.NoExtractableText();
            }

            return PdfSearchResult.NoMatches();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PdfDocumentEncryptedException ex)
        {
            return PdfSearchResult.DocumentEncrypted(ex.Message);
        }
        catch (Exception ex) when (IsLikelyEncryptionFailure(ex))
        {
            return PdfSearchResult.DocumentEncrypted(ex.Message);
        }
        catch (Exception ex)
        {
            return PdfSearchResult.Failed(ex.Message);
        }
    }

    private static void CollectPhraseHits(
        List<PdfSearchHit> hits,
        int pageIndex,
        string haystack,
        string rawText,
        string needle,
        StringComparison comparison)
    {
        var before = hits.Count;
        CollectSubstringHits(hits, pageIndex, haystack, rawText, needle, comparison);

        // Imperfect extractors often drop spaces ("helloworld" for "hello world").
        // Fall back to a whitespace-insensitive scan when the spaced phrase missed.
        if (hits.Count == before && needle.Contains(' '))
        {
            var compactHaystack = RemoveAllWhitespace(haystack);
            var compactNeedle = RemoveAllWhitespace(needle);
            if (compactNeedle.Length > 0)
            {
                CollectSubstringHits(hits, pageIndex, compactHaystack, rawText, compactNeedle, comparison);
            }
        }
    }

    private static void CollectSubstringHits(
        List<PdfSearchHit> hits,
        int pageIndex,
        string haystack,
        string rawText,
        string needle,
        StringComparison comparison)
    {
        var searchFrom = 0;
        while (searchFrom < haystack.Length)
        {
            var found = haystack.IndexOf(needle, searchFrom, comparison);
            if (found < 0)
            {
                break;
            }

            // Map normalized index back approximately into the raw snippet source.
            var snippetSource = rawText.Length > 0 ? rawText : haystack;
            var snippetStart = Math.Min(found, Math.Max(0, snippetSource.Length - 1));
            hits.Add(new PdfSearchHit(
                PageIndex: pageIndex,
                Snippet: PdfSearchSnippet.Build(snippetSource, snippetStart, needle.Length),
                MatchStart: found,
                MatchLength: needle.Length));

            searchFrom = found + Math.Max(1, needle.Length);
        }
    }

    private static string RemoveAllWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
    }

    /// <summary>
    /// Collapse runs of whitespace so imperfect extraction order that injects
    /// extra spaces/newlines still matches ordinary phrases.
    /// </summary>
    public static string NormalizeForSearch(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var chars = new char[text.Length];
        var length = 0;
        var previousWasSpace = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasSpace && length > 0)
                {
                    chars[length++] = ' ';
                    previousWasSpace = true;
                }

                continue;
            }

            chars[length++] = ch;
            previousWasSpace = false;
        }

        if (length > 0 && chars[length - 1] == ' ')
        {
            length--;
        }

        return new string(chars, 0, length);
    }

    private static bool IsLikelyEncryptionFailure(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        return message.Contains("password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("encrypt", StringComparison.OrdinalIgnoreCase)
            || ex.GetType().Name.Contains("Encrypt", StringComparison.OrdinalIgnoreCase);
    }
}
