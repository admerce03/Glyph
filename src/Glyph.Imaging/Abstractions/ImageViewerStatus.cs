namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Image viewer mode/selection/resize/markup status strings (F26–F34).
/// </summary>
public static class ImageViewerStatus
{
    public const string AnimationPlaying = "Animation playing.";
    public const string AnimationPlayingAutoplay = "Animation playing (autoplay).";
    public const string FolderNavUnavailable = "Folder navigation unavailable.";
    public const string DrawModeOff = "Draw mode off.";
    public const string CannotMoveInverted =
        "Cannot move an inverted selection — Invert again first.";
    public const string MovingSelection = "Moving selection…";
    public const string InvertedFullEmpty = "Inverted full selection → empty.";
    public const string NothingToResize = "Nothing to resize.";
    public const string ResizeNeedsPositive =
        "Resize needs positive width and height.";
    public const string BatchResizeNeedsScale =
        "Batch resize needs a positive Scale %.";
    public const string BatchNeedsFolder =
        "Batch folder ops need a folder with multiple images.";
    public const string AdjustmentsCancelled = "Adjustments cancelled.";
    public const string NoAdjustments = "No adjustments to apply.";
    public const string AdjustmentsApplied = "Color adjustments applied.";
    public const string SignatureLibraryUnavailable = "Signature library unavailable.";
    public const string NoSignaturesSaved =
        "No signatures saved — add one from a PDF Sign toolbar first.";
    public const string SaveCancelledMarkup =
        "Save cancelled — markup still on overlay.";
    public const string FolderOcrUnavailable = "Folder OCR unavailable.";
    public const string FolderOcrCopied = "Folder OCR text copied.";
    public const string RunningOcr = "Running OCR…";
    public const string SaveFrameFailedPrefix = "Save frame failed: ";
    public const string CopyImageFailedPrefix = "Copy image failed: ";
    public const string PasteFailedPrefix = "Paste failed: ";
    public const string EditFailedPrefix = "Edit failed: ";
    public const string BackgroundToolsFailedPrefix = "Background tools failed: ";
    public const string PreviewFailedPrefix = "Preview failed: ";
    public const string AdjustFailedPrefix = "Adjust failed: ";
    public const string StampFailedPrefix = "Stamp failed: ";
    public const string PrintFailedPrefix = "Print failed: ";
    public const string MetadataFailedPrefix = "Metadata failed: ";
    public const string SaveFailedPrefix = "Save failed: ";
    public const string ExportFailedPrefix = "Export failed: ";

    public static string FormatCopiedImage(int width, int height) =>
        $"Copied image {width}×{height}.";

    public static string FormatLoadingPreview(int nativeMaxEdge) =>
        $"Loading full preview… ({nativeMaxEdge:N0}px edge)";

    public static string FormatSelected(int width, int height) =>
        $"Selected {width}×{height} px";

    public static string FormatLassoSelected(int width, int height, int pointCount) =>
        $"Lasso selected {width}×{height} px ({pointCount} pts)";

    public static string FormatSelectionMapped(string kindLabel, int width, int height) =>
        $"Selection ({kindLabel}) → {width}×{height} px";

    public static string FormatSubjectCopied(int width, int height) =>
        $"Subject copied ({width}×{height}).";

    public static string FormatMarkupTool(string toolName) =>
        $"{toolName} markup — drag on image (Esc exits; Ctrl+Z undoes).";

    public static string FormatTextPlaced(int total) =>
        $"Text placed ({total} total).";

    public static string FormatMarkupAdded(string kind, int total) =>
        $"Markup {kind} added ({total} total).";

    public static string FormatMarkupStrokeAdded(int total) =>
        $"Markup stroke added ({total} total).";

    public static string FormatPrintSkipped(string fileName, string message) =>
        $"Print skipped {fileName}: {message}";

    public static string FormatFailed(string prefix, string message) => prefix + message;

    public static string FormatSubjectSaved(string fileName) =>
        "Subject saved → " + fileName;

    public static string FormatExported(string fileName) =>
        "Exported " + fileName;

    public const string AlreadyFirst = "Already at first image.";
    public const string AlreadyLast = "Already at last image.";
    public const string AnimationFinishedLooping = "Animation finished looping.";
    public const string ToolbarShown = "Toolbar shown.";
    public const string ToolbarHidden = "Toolbar hidden.";
    public const string FolderOcrCancelled = "Folder OCR cancelled.";
    public const string FolderOcrNoResults = "Folder OCR produced no results.";
    public const string OcrFinishedNoText = "OCR finished — no text.";
    public const string MarkupUndone = "Markup undone.";
    public const string CalloutMarkupPrompt =
        "Callout markup — drag a box on the image (Esc exits).";
    public const string TextMarkupPrompt =
        "Text markup — click on image to place (Esc exits).";

    public static string FormatSelectionInverted(int width, int height) =>
        $"Selection inverted — copy/cut/delete apply outside {width}×{height}.";

    public static string FormatSelectionRestored(string kindLabel, int width, int height) =>
        $"Selection restored ({kindLabel} {width}×{height}).";

    public static string FormatOcrFinished(int lineCount) =>
        $"OCR finished — {lineCount} line(s).";

    public static string FormatFolderOcrCancelledAfter(int updated) =>
        $"Folder OCR cancelled after {updated} image(s).";

    public static string FormatFolderOcrFinished(int updated) =>
        $"Folder OCR finished — {updated} image(s).";

    public static string FormatMarkupUndone(int remaining) =>
        remaining == 0 ? MarkupUndone : $"Markup undone ({remaining} left).";

    public static string FormatPrintUiShown(int imageCount, int pagesPerSheet) =>
        $"Print UI shown · {imageCount} image(s)"
        + (pagesPerSheet > 1 ? $" · {pagesPerSheet}-up." : ".");

    public const string RotatedLeft = "Rotated left.";
    public const string RotatedRight = "Rotated right.";
    public const string Rotated180 = "Rotated 180°.";
    public const string OrientationNormalized = "Orientation normalized.";
    public const string Straightened = "Straightened (deskew).";
    public const string FlippedHorizontally = "Flipped horizontally.";
    public const string FlippedVertically = "Flipped vertically.";
    public const string AssignedSrgbIcc = "Assigned sRGB ICC profile.";
    public const string ConvertedPixelsToSrgb = "Converted pixels to sRGB.";
    public const string DescriptiveMetadataUpdated = "Descriptive metadata updated (IPTC).";
    public const string WithCurrentSuffix = " (+ current).";

    public static string FormatMovedSelection(int destX, int destY) =>
        $"Moved selection to ({destX},{destY}).";

    public static string FormatPastedClipboardImage(int destX, int destY) =>
        $"Pasted clipboard image at ({destX},{destY}).";

    public static string FormatPastedRect(int width, int height, int destX, int destY) =>
        $"Pasted {width}×{height} at ({destX},{destY}).";

    public static string FormatClearedOutsideSelection(int width, int height) =>
        $"Cleared outside selection (kept {width}×{height}).";

    public static string FormatClearedSelection(int width, int height) =>
        $"Cleared selection {width}×{height}.";

    public static string FormatResized(int width, int height, double dpi) =>
        $"Resized to {width}×{height} @ {dpi:0.#} DPI.";

    public static string FormatResizedCurrentWithBatch(
        int width,
        int height,
        int batchCount,
        double pct) =>
        $"Resized current to {width}×{height}; batch-updated {batchCount} folder image(s) at {pct:0.#}%.";

    public static string FormatConvertedCurrent(string kind) =>
        $"Converted current image → {kind}.";

    public static string FormatAssignedProfile(string kind) =>
        $"Assigned {kind} profile to current image.";

    public static string FormatCurrentImage(string label) =>
        $"Current image: {label}.";

    public static string FormatStamped(string name, int destX, int destY) =>
        $"Stamped “{name}” at ({destX},{destY}).";

    public static string FormatBatchCancelled(string label, int updated) =>
        $"Batch {label} cancelled after {updated} file(s).";

    public static string FormatBatchUpdated(string label, int updated, bool includeCurrent) =>
        $"Batch {label}: updated {updated} folder image(s)"
        + (includeCurrent ? WithCurrentSuffix : ".");

    public static string FormatSelectedPixels(int width, int height, string kindLabel) =>
        $"Selected {width}×{height} px ({kindLabel})";

    public static string FormatBackgroundRemoved(int fuzz, bool trimmed) =>
        trimmed
            ? $"Background removed (fuzz {fuzz:0}%) and trimmed."
            : $"Background removed (fuzz {fuzz:0}%).";
}
