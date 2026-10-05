namespace Glyph.Core.Documents;

/// <summary>
/// Status for in-document Find (F06) and entity detection feedback.
/// </summary>
public static class PdfFindStatus
{
    public const string NoResults = "No search results.";
    public const string Searching = "Searching…";
    public const string NoExtractableText = "No extractable text in document.";
    public const string NoExtractableTextToCopy = "No extractable text to copy.";
    public const string NoEntitiesDetected =
        "No URLs, emails, phones, addresses, dates, or times detected.";
    public const string OpenedCalendarInvite = "Opened calendar invite.";

    public static string FormatCopiedEntity(string kind) =>
        $"Copied {kind}.";

    public static string FormatCopiedEntityUnparsed(string kind) =>
        $"Copied {kind} (could not parse for calendar).";

    public static string FormatFollowedLink(int pageNumber1Based) =>
        $"Followed link to page {pageNumber1Based}.";
}

/// <summary>
/// User bookmark and outline export status (F03).
/// </summary>
public static class BookmarkStatus
{
    public const string AddBeforeExport =
        "Add bookmarks before exporting to the PDF outline.";
    public const string WritingOutline = "Writing PDF outline…";
    public const string Renamed = "Bookmark renamed.";
    public const string Deleted = "Bookmark deleted.";

    public static string FormatBookmarked(string title) =>
        $"Bookmarked “{title}”.";
}

/// <summary>
/// Attachment save status (F01 / sidebar attachments).
/// </summary>
public static class AttachmentSaveStatus
{
    public const string Cancelled = "Attachment save cancelled.";

    public static string FormatSaved(string name, int byteLength) =>
        $"Saved attachment “{name}” ({byteLength} bytes).";
}

/// <summary>
/// Annotation group / resize / style-slider status.
/// </summary>
public static class AnnotationGroupStatus
{
    public const string SamePageOnly =
        "Grouping is limited to annotations on the same page.";
    public const string SelectionNotGrouped = "Selection is not grouped.";
    public const string UpdatingLineEndpoints = "Updating line endpoints…";
    public const string LineEndpointsUpdated = "Line endpoints updated.";
    public const string Resizing = "Resizing annotation…";
    public const string Rotating = "Rotating annotation…";
    public const string AreaHighlightCancelled = "Area highlight cancelled.";
    public const string InkStrokeAdded = "Ink stroke added.";
    public const string AddingCallout = "Adding callout…";
    public const string FinishPolygonFirst =
        "Finish the current page polygon first (Enter), or Esc to cancel.";

    public static string FormatGrouped(int count) =>
        $"Grouped {count} annotations.";

    public static string FormatUngrouped(int count) =>
        $"Ungrouped {count} annotation(s).";

    public static string FormatAlignment(string quadding) =>
        $"Alignment set to {quadding}.";

    public static string FormatStrokeWidth(double width) =>
        $"Stroke width set to {width:0.#} pt.";

    public static string FormatOpacity(int percent) =>
        $"Opacity set to {percent}%.";

    public static string FormatThumbnailSize(double widthPx) =>
        $"Thumbnail size {widthPx:0}px.";

    public static string FormatReplacedStroke(string label) =>
        $"Replaced stroke with cleaned {label}.";
}

/// <summary>
/// AcroForm button / field activation status.
/// </summary>
public static class FormFieldActionStatus
{
    public const string AutoFillEmpty =
        "AutoFill profile is empty — choose Edit AutoFill profile first.";
    public const string ProfileEditCancelled = "Profile edit cancelled.";
    public const string UnsupportedButtonScheme =
        "Button link uses an unsupported scheme.";
    public const string ApplyCancelled = "Apply cancelled.";

    public static string FormatFocused(string name, string kind) =>
        $"Focused {name} ({kind}).";

    public static string FormatOpened(string caption) =>
        $"Opened {caption}.";

    public static string FormatButtonToPage(string caption, int pageNumber1Based) =>
        $"Button {caption} → page {pageNumber1Based}.";

    public static string FormatButtonNoAction(string caption) =>
        $"Button {caption}: no activatable action.";

    public static string FormatSelectedRadio(string name) =>
        $"Selected radio {name}.";

    public static string FormatUnsupportedEdit(string kind) =>
        $"Editing {kind} fields is not supported yet.";
}

/// <summary>
/// Page extract / clipboard paste status for page editor.
/// </summary>
public static class PageClipboardStatus
{
    public const string NoPagesOnClipboard = "No pages on the clipboard.";
    public const string ClipboardPagesUnavailable = "Clipboard pages unavailable.";
    public const string ExtractCancelled = "Extract cancelled.";
    public const string Extracting = "Extracting…";
    public const string NeedTwoPagesToSplit = "Need at least two pages to split.";

    public static string FormatInserting(string fileName) =>
        $"Inserting {fileName}…";
}
