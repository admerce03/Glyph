namespace Glyph.Pdf.Abstractions;

/// <summary>One flat outline item to embed as a standard PDF bookmark.</summary>
public sealed record PdfOutlineExportEntry(string Title, int PageIndex);

/// <summary>
/// Writes user bookmarks into the PDF /Outlines tree (F09-08).
/// </summary>
public interface IPdfOutlineExportService
{
    Task ExportAsync(
        IPdfDocument document,
        IReadOnlyList<PdfOutlineExportEntry> entries,
        CancellationToken cancellationToken = default);
}
