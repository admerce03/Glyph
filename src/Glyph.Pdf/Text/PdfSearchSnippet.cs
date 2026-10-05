namespace Glyph.Pdf.Text;

/// <summary>
/// Builds ellipsis-padded snippets around a search match for result lists (F06-10).
/// </summary>
public static class PdfSearchSnippet
{
    public const int DefaultPad = 28;

    public static string Build(string text, int matchStart, int matchLength, int pad = DefaultPad)
    {
        if (string.IsNullOrEmpty(text) || matchLength <= 0 || matchStart >= text.Length)
        {
            return string.Empty;
        }

        var start = Math.Max(0, matchStart - pad);
        var end = Math.Min(text.Length, Math.Max(matchStart, 0) + matchLength + pad);
        if (start >= end)
        {
            return string.Empty;
        }

        var snippet = text[start..end].Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (snippet.Length == 0)
        {
            return string.Empty;
        }

        if (start > 0)
        {
            snippet = "…" + snippet;
        }

        if (end < text.Length)
        {
            snippet += "…";
        }

        return snippet;
    }
}
