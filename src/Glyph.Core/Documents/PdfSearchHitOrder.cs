namespace Glyph.Core.Documents;

/// <summary>
/// Find-results ordering (F06-12): page order or relevance.
/// </summary>
public static class PdfSearchHitOrder
{
    public const bool SupportsPageOrder = true;

    public const bool SupportsRelevance = true;

    public enum SortMode
    {
        PageOrder = 0,
        Relevance = 1,
    }

    public static IReadOnlyList<T> ByPageThenOccurrence<T>(
        IEnumerable<T> hits,
        Func<T, int> pageIndex,
        Func<T, int>? occurrenceIndex = null)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(pageIndex);

        if (occurrenceIndex is null)
        {
            return hits.OrderBy(pageIndex).ToArray();
        }

        return hits.OrderBy(pageIndex).ThenBy(occurrenceIndex).ToArray();
    }

    /// <summary>
    /// Higher scores rank first: whole-word and case-exact matches beat substrings;
    /// earlier match offsets and earlier pages break ties.
    /// </summary>
    public static int ScoreRelevance(
        string query,
        string snippet,
        int matchStart,
        int matchLength,
        bool caseSensitive = false)
    {
        query ??= string.Empty;
        snippet ??= string.Empty;
        if (query.Length == 0 || matchLength <= 0 || matchStart < 0 || matchStart >= snippet.Length)
        {
            return 0;
        }

        var end = Math.Min(snippet.Length, matchStart + matchLength);
        var matched = snippet[matchStart..end];
        var comparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var score = 10;
        if (string.Equals(matched, query, comparison))
        {
            score += 40;
        }

        if (string.Equals(matched, query, StringComparison.Ordinal))
        {
            score += 15; // exact case
        }

        var beforeOk = matchStart == 0 || !IsWordChar(snippet[matchStart - 1]);
        var afterOk = end >= snippet.Length || !IsWordChar(snippet[end]);
        if (beforeOk && afterOk)
        {
            score += 50; // whole word
        }
        else if (beforeOk)
        {
            score += 25; // word prefix
        }

        // Prefer matches nearer the start of the snippet context.
        score += Math.Max(0, 20 - Math.Min(20, matchStart));
        return score;
    }

    public static IReadOnlyList<T> ByRelevance<T>(
        IEnumerable<T> hits,
        string query,
        Func<T, string> snippet,
        Func<T, int> matchStart,
        Func<T, int> matchLength,
        Func<T, int> pageIndex,
        bool caseSensitive = false)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(snippet);
        ArgumentNullException.ThrowIfNull(matchStart);
        ArgumentNullException.ThrowIfNull(matchLength);
        ArgumentNullException.ThrowIfNull(pageIndex);

        return hits
            .OrderByDescending(h => ScoreRelevance(
                query,
                snippet(h),
                matchStart(h),
                matchLength(h),
                caseSensitive))
            .ThenBy(pageIndex)
            .ThenBy(matchStart)
            .ToArray();
    }

    public static IReadOnlyList<T> Apply<T>(
        SortMode mode,
        IEnumerable<T> hits,
        string query,
        Func<T, string> snippet,
        Func<T, int> matchStart,
        Func<T, int> matchLength,
        Func<T, int> pageIndex,
        Func<T, int>? occurrenceIndex = null,
        bool caseSensitive = false)
    {
        return mode == SortMode.Relevance
            ? ByRelevance(hits, query, snippet, matchStart, matchLength, pageIndex, caseSensitive)
            : ByPageThenOccurrence(hits, pageIndex, occurrenceIndex ?? matchStart);
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
