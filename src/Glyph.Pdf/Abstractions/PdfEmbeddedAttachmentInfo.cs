namespace Glyph.Pdf.Abstractions;

/// <summary>
/// One document-level embedded file attachment (PDF name tree / EmbeddedFiles).
/// </summary>
public sealed record PdfEmbeddedAttachmentInfo(
    int Index,
    string Name,
    long? SizeBytes);
