namespace Glyph.Pdf.Abstractions;

public interface IPdfOutlineService
{
    Task<IReadOnlyList<PdfOutlineNode>> GetOutlineAsync(
        IPdfDocument document,
        CancellationToken cancellationToken = default);
}
