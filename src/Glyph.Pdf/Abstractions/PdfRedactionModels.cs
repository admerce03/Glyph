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
    /// Clear Info dictionary Title/Author/Subject/Keywords/Creator/Producer/CreationDate/ModDate via incremental patch.
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

/// <summary>User-facing copy for redaction confirm/apply (F21-07).</summary>
public static class PdfRedactionUiCopy
{
    public const string ApplyDialogTitle = "Apply redactions permanently?";
    public const string DialogTitle = "Redaction";
    public const string ModeOff = "Redact mode off.";
    public const string ModeEnter =
        "Redact mode — drag a rectangle to mark. Esc to exit.";
    public const string CancelledStatus = "Redaction cancelled.";
    public const string ClearedPendingStatus = "Cleared pending redactions.";
    public const string ApplyingStatus = "Applying redactions…";
    public const string DrawMarksButton = "Draw marks";
    public const string ApplyButton = "Apply…";
    public const string MarkSelectionButton = "Mark selection";
    public const string MarkRegionButton = "Mark region";
    public const string MarkForRedactionMenu = "Mark for redaction";
    public const string MarkRegionForRedactionMenu = "Mark region for redaction";
    public const string MarkTooSmallStatus = "Redaction mark too small.";
    public const string SelectTextPrompt = "Select text to mark for redaction.";
    public const string DragRegionPrompt = "Drag a region first.";
    public const string MarkFailedPrefix = "Mark failed: ";
    public const string ApplyFailedPrefix = "Apply failed: ";
    public const string NothingToApplyStatus = "Nothing to apply.";
    public const string RemovedMarkStatus = "Removed redaction mark.";
    public const string UndidMarkStatus = "Undid pending redaction mark.";
    public const string ToolbarTooltip =
        "Mark areas/text for redaction; apply permanently removes content";
    public const string PendingOverlayTooltip = "Click to remove pending redaction";
    public const string EmptyIntro =
        "Mark areas to remove permanently. Drag rectangles in draw mode, or mark the current selection/region.";

    public static string FormatApplyBody(int pendingCount) =>
        $"Apply {pendingCount} redaction mark(s)? Underlying text and covered image content will be removed. This cannot be undone.";

    public static string FormatPendingStatus(int pendingCount) =>
        pendingCount <= 0
            ? "No pending redactions."
            : $"{pendingCount} pending mark(s). Apply permanently removes underlying text/images — this cannot be undone.";

    public static string ModeOffWithPending(int pendingCount) =>
        pendingCount <= 0
            ? ModeOff
            : $"Redact mode off — {pendingCount} pending mark(s). Use Redact → Apply when ready.";

    public static string MarkFindMatchesButton(int hitCount) =>
        $"Mark find matches ({hitCount})";

    public static string FormatMarkedStatus(int pendingCount) =>
        $"Marked redaction ({pendingCount} pending). Redact → Apply when ready.";

    public static string FormatMarkedTextStatus(int pendingCount) =>
        $"Marked text for redaction ({pendingCount} pending).";

    public static string FormatMarkedRegionStatus(int pendingCount) =>
        $"Marked region for redaction ({pendingCount} pending).";

    public static string FormatMarkFailed(string message) =>
        MarkFailedPrefix + message;

    public static string FormatApplyFailed(string message) =>
        ApplyFailedPrefix + message;

    public static string FormatRemovedStatus(int remainingPending) =>
        remainingPending <= 0
            ? RemovedMarkStatus
            : $"Removed redaction mark ({remainingPending} pending).";

    public static string FormatUndidStatus(int remainingPending) =>
        remainingPending <= 0
            ? UndidMarkStatus
            : $"Undid pending redaction ({remainingPending} remaining).";

    public static string FormatAppliedStatus(
        int marksApplied,
        int pagesChanged,
        int textObjectsRemoved,
        int imageObjectsRemoved,
        int annotationsRemoved,
        int attachmentsRemoved,
        bool metadataCleared) =>
        $"Applied {marksApplied} redaction(s) on {pagesChanged} page(s); "
        + $"removed {textObjectsRemoved} text / {imageObjectsRemoved} image / "
        + $"{annotationsRemoved} annotation / {attachmentsRemoved} attachment object(s)"
        + (metadataCleared ? "; metadata cleared" : "")
        + ".";
}
