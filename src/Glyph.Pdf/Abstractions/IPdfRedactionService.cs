namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Pending redaction marks and permanent apply (content removal, not cover-only).
/// </summary>
public interface IPdfRedactionService
{
    IReadOnlyList<PdfPendingRedaction> GetPending(IPdfDocument document);

    PdfPendingRedaction MarkRectangle(IPdfDocument document, int pageIndex, PdfRect bounds, string? label = null);

    PdfPendingRedaction MarkTextRegion(IPdfDocument document, int pageIndex, PdfRect bounds, string? label = null);

    bool RemovePending(IPdfDocument document, Guid redactionId);

    /// <summary>Undo the most recently added pending mark (F49-13).</summary>
    PdfPendingRedaction? UndoLastPending(IPdfDocument document);

    void ClearPending(IPdfDocument document);

    Task<PdfRedactionApplyResult> ApplyAsync(
        IPdfDocument document,
        PdfRedactionApplyOptions? options = null,
        CancellationToken cancellationToken = default);
}
