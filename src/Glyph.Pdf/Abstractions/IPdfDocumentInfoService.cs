namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Reads and updates document metadata and encryption/permission summary.
/// </summary>
public interface IPdfDocumentInfoService
{
    PdfDocumentInfo GetInfo(IPdfDocument document);

    /// <summary>
    /// Writes Info dictionary fields (title/author/subject/keywords) into the open document
    /// via an incremental PDF update, then reloads the in-memory handle.
    /// </summary>
    void SetInfo(IPdfDocument document, PdfDocumentInfoUpdate update);
}
