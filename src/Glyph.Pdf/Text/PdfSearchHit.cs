namespace Glyph.Pdf.Text;

public sealed record PdfSearchHit(
    int PageIndex,
    string Snippet,
    int MatchStart,
    int MatchLength);
