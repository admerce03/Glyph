namespace Glyph.Pdf.Text;

/// <summary>
/// Outcome of a PDF text-layer search. Distinguishes "no matches" from
/// documents that have no extractable text (scanned/image-only) or that
/// require a password.
/// </summary>
public enum PdfSearchStatus
{
    Success,
    EmptyQuery,
    NoMatches,
    NoExtractableText,
    DocumentEncrypted,
    Cancelled,
    Failed,
}
