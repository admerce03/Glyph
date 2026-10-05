namespace Glyph.Pdf.Text;

/// <summary>
/// Default status-bar strings for <see cref="PdfSearchStatus"/> (F06).
/// </summary>
public static class PdfSearchStatusText
{
    public static string Format(PdfSearchStatus status, int matchCount = 0, string? message = null)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        return status switch
        {
            PdfSearchStatus.EmptyQuery => "Enter search text.",
            PdfSearchStatus.NoMatches => "No matches.",
            PdfSearchStatus.NoExtractableText => "OCR required.",
            PdfSearchStatus.DocumentEncrypted => "Password required.",
            PdfSearchStatus.Failed => "Search failed.",
            PdfSearchStatus.Cancelled => "Search cancelled.",
            PdfSearchStatus.Success => matchCount == 1 ? "1 match" : $"{matchCount} matches",
            _ => string.Empty,
        };
    }
}
