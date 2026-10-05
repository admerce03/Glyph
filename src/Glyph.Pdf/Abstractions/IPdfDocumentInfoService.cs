namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Reads document metadata and encryption/permission summary.
/// </summary>
public interface IPdfDocumentInfoService
{
    PdfDocumentInfo GetInfo(IPdfDocument document);
}
