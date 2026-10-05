namespace Glyph.Core.Documents;

/// <summary>
/// Parses 1-based print/export page range text (e.g. <c>1-3,5</c>) into sorted unique
/// zero-based page indexes (F44-03).
/// </summary>
public static class PageRangeParser
{
    public static IReadOnlyList<int> Parse(string? text, int pageCount)
    {
        var result = new SortedSet<int>();
        if (string.IsNullOrWhiteSpace(text) || pageCount <= 0)
        {
            return [];
        }

        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Contains('-', StringComparison.Ordinal))
            {
                var bounds = part.Split('-', 2, StringSplitOptions.TrimEntries);
                if (bounds.Length == 2
                    && int.TryParse(bounds[0], out var start)
                    && int.TryParse(bounds[1], out var end))
                {
                    if (start > end)
                    {
                        (start, end) = (end, start);
                    }

                    for (var p = start; p <= end; p++)
                    {
                        if (p >= 1 && p <= pageCount)
                        {
                            result.Add(p - 1);
                        }
                    }
                }

                continue;
            }

            if (int.TryParse(part, out var one) && one >= 1 && one <= pageCount)
            {
                result.Add(one - 1);
            }
        }

        return result.ToList();
    }
}
