namespace Glyph.Core.Documents;

/// <summary>
/// Parses the goto-box 1-based page number into a zero-based index (F04-14/15).
/// </summary>
public static class PageGotoParser
{
    /// <summary>
    /// Tries to parse <paramref name="text"/> as a 1-based page number in
    /// <c>1..pageCount</c>. Returns the zero-based index, or <c>null</c> when invalid.
    /// </summary>
    public static int? TryParseZeroBased(string? text, int pageCount)
    {
        if (pageCount <= 0 || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!int.TryParse(text.Trim(), out var pageNumber))
        {
            return null;
        }

        if (pageNumber < 1 || pageNumber > pageCount)
        {
            return null;
        }

        return pageNumber - 1;
    }
}
