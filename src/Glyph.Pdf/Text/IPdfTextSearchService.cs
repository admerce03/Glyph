namespace Glyph.Pdf.Text;

public interface IPdfTextSearchService
{
    Task<IReadOnlyList<PdfSearchHit>> SearchAsync(
        string path,
        string query,
        bool caseSensitive = false,
        CancellationToken cancellationToken = default);
}
