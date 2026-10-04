namespace Glyph.Pdf.Abstractions;

public interface IPdfLinkService
{
    Task<IReadOnlyList<PdfLink>> GetPageLinksAsync(
        IPdfDocument document,
        int pageIndex,
        CancellationToken cancellationToken = default);
}
