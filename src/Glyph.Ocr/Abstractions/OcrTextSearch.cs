namespace Glyph.Ocr.Abstractions;

/// <summary>
/// Finds OCR word indices whose text contains a query (ordinal ignore-case by default).
/// </summary>
public static class OcrTextSearch
{
    public static IReadOnlyList<int> FindWordIndexes(
        IEnumerable<OcrWord> words,
        string? query,
        bool caseSensitive = false)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var comparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var needle = query.Trim();
        var hits = new List<int>();
        var index = 0;
        foreach (var word in words)
        {
            if (word.Text.Contains(needle, comparison))
            {
                hits.Add(index);
            }

            index++;
        }

        return hits;
    }

    public static IEnumerable<OcrWord> FlattenWords(OcrResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        foreach (var line in result.Lines)
        {
            foreach (var word in line.Words)
            {
                yield return word;
            }
        }
    }
}
