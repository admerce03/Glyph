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

    /// <summary>
    /// Lists document-level embedded file attachments (name + size when available).
    /// </summary>
    IReadOnlyList<PdfEmbeddedAttachmentInfo> ListAttachments(IPdfDocument document);

    /// <summary>
    /// Extracts the raw bytes of an embedded attachment by index.
    /// </summary>
    byte[] GetAttachmentBytes(IPdfDocument document, int index);
}
