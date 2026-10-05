namespace Glyph.Core.Documents;

/// <summary>
/// Status after mutating PDF annotations (color/size/rotate/author/edit/erase).
/// </summary>
public static class AnnotationMutationStatus
{
    public const string ColorUpdated = "Annotation color updated.";
    public const string Resized = "Annotation resized.";
    public const string Rotated90 = "Annotation rotated 90°.";
    public const string EditCancelled = "Edit cancelled.";
    public const string FillCleared = "Fill cleared.";
    public const string FillColorUpdated = "Fill color updated.";
    public const string AnnotationMoved = "Annotation moved.";
    public const string UnderlineOn = "Underline on.";
    public const string UnderlineOff = "Underline off.";
    public const string AutoFillProfileSaved = "AutoFill profile saved.";
    public const string AutoFillProfileCleared = "AutoFill profile cleared.";
    public const string AutoFillNoEmptyFields = "AutoFill found no empty matching fields.";
    public const string DocumentInfo = "Document info.";
    public const string ToolbarShown = "Toolbar shown.";
    public const string ToolbarHidden = "Toolbar hidden.";
    public const string RegionSelectedCopyHint =
        "Region selected — right-click to copy as image.";

    public static string FormatAuthorSet(string author) =>
        $"Annotation author set to {author}.";

    public static string FormatErased(string label) =>
        $"Erased {label}.";

    public static string FormatUndid(string label) =>
        $"Undid {label}.";

    public static string FormatUpdated(string label) =>
        $"Updated {label}.";

    public static string FormatDuplicated(string label) =>
        $"Duplicated {label}.";

    public static string FormatAutoFillUpdated(int filled) =>
        $"AutoFill updated {filled} field(s).";

    public static string FormatFlattenedAnnotations(int pagesChanged) =>
        pagesChanged == 1
            ? "Flattened annotations on 1 page."
            : $"Flattened annotations on {pagesChanged} pages.";

    public static string FormatFlattenedWithFailures(int pagesChanged, int pagesFailed) =>
        $"Flattened {pagesChanged} page(s); {pagesFailed} failed.";

    public static string FormatMovedAnnotations(int count) =>
        $"Moved {count} annotations.";

    public static string FormatDeletedAnnotations(int count) =>
        $"Deleted {count} annotations.";

    public static string FormatDeletedAnnotation(string label) =>
        $"Deleted {label}.";

    public static string FormatSelectedAnnotation(string label) =>
        $"Selected {label}.";

    public static string FormatSelectedText(string trimmed) =>
        $"Selected “{trimmed}”";

    public static string FormatPageIndex(int pageIndex1Based) =>
        $"Page {pageIndex1Based}";

    public static string FormatChromeStatus(
        int pageIndex1Based,
        int pageCount,
        int zoomPercent,
        string layoutMode,
        string encryptedSuffix) =>
        $"Page {pageIndex1Based} / {pageCount}    Zoom {zoomPercent}%    {layoutMode}{encryptedSuffix}";

    public static string FormatSelectedAnnotationsMoveTogether(int count) =>
        $"Selected {count} annotations. Drag to move together.";

    public static string FormatSelectedAnnotationDrag(string label) =>
        $"Selected {label}. Drag to move; handles resize.";

    public static string FormatAdjustingEndpoint(string label) =>
        $"Adjusting {label} endpoint…";

    public static string FormatResizing(string label) =>
        $"Resizing {label}…";
}
