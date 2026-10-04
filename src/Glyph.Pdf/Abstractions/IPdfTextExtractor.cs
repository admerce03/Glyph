namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Extracts selectable text geometry from PDF pages. Implementations must be
/// cancelable and must not require rasterizing the page.
/// </summary>
public interface IPdfTextExtractor
{
    Task<IReadOnlyList<PdfTextChar>> GetCharsAsync(
        IPdfDocument document,
        int pageIndex,
        CancellationToken cancellationToken = default);

    Task<string> GetTextAsync(
        IPdfDocument document,
        int pageIndex,
        CancellationToken cancellationToken = default);
}
