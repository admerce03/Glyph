namespace Glyph.Pdf.Abstractions;

public enum PdfRedactionKind
{
    Rectangle,
    Text,
}

/// <summary>
/// A pending (not yet applied) redaction mark in PDF user space.
/// </summary>
public sealed record PdfPendingRedaction(
    Guid Id,
    int PageIndex,
    PdfRedactionKind Kind,
    PdfRect Bounds,
    string? Label = null);

public sealed record PdfRedactionApplyOptions(
    bool RemoveIntersectingTextObjects = true,
    bool RemoveIntersectingImageObjects = false,
    bool RemoveIntersectingAnnotations = true,
    /// <summary>
    /// Unlink document-level embedded file attachments from the name tree
    /// (PDFium may leave orphan stream bytes until a later full optimize pass).
    /// </summary>
    bool RemoveEmbeddedAttachments = true,
    /// <summary>
    /// Clear Info dictionary Title/Author/Subject/Keywords/Creator/Producer via incremental patch.
    /// </summary>
    bool RemoveMetadata = true);

public sealed record PdfRedactionApplyResult(
    int MarksApplied,
    int PagesChanged,
    int TextObjectsRemoved,
    int ImageObjectsRemoved,
    int AnnotationsRemoved = 0,
    int AttachmentsRemoved = 0,
    bool MetadataCleared = false);
