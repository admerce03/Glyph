namespace Glyph.Pdf.Text;

public sealed record PdfSearchResult(
    PdfSearchStatus Status,
    IReadOnlyList<PdfSearchHit> Hits,
    string? Message = null)
{
    public static PdfSearchResult EmptyQuery() =>
        new(PdfSearchStatus.EmptyQuery, Array.Empty<PdfSearchHit>(), "Enter search text.");

    public static PdfSearchResult NoMatches() =>
        new(PdfSearchStatus.NoMatches, Array.Empty<PdfSearchHit>(), "No matches.");

    public static PdfSearchResult Success(IReadOnlyList<PdfSearchHit> hits) =>
        new(PdfSearchStatus.Success, hits);

    public static PdfSearchResult NoExtractableText() =>
        new(
            PdfSearchStatus.NoExtractableText,
            Array.Empty<PdfSearchHit>(),
            "This PDF has no extractable text. OCR is required to search scanned or image-only pages.");

    public static PdfSearchResult DocumentEncrypted(string? detail = null) =>
        new(
            PdfSearchStatus.DocumentEncrypted,
            Array.Empty<PdfSearchHit>(),
            detail ?? "This PDF is password-protected and could not be searched.");

    public static PdfSearchResult Cancelled() =>
        new(PdfSearchStatus.Cancelled, Array.Empty<PdfSearchHit>(), "Search cancelled.");

    public static PdfSearchResult Failed(string message) =>
        new(PdfSearchStatus.Failed, Array.Empty<PdfSearchHit>(), message);
}
