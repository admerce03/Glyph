namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Result of flattening annotations into page content.
/// </summary>
public sealed record PdfFlattenResult(int PagesProcessed, int PagesChanged, int PagesFailed);
