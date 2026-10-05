namespace Glyph.Core.Documents;

/// <summary>
/// Status strings for page edit operations (rotate/delete/reorder/blank/duplicate/crop/merge/split/undo).
/// </summary>
public static class PageEditStatus
{
    public const string Reordering = "Reordering…";
    public const string PagesReordered = "Pages reordered.";
    public const string Rotating = "Rotating…";
    public const string Deleting = "Deleting…";
    public const string CannotDeleteEveryPage = "Cannot delete every page.";
    public const string InsertingBlankPage = "Inserting blank page…";
    public const string InsertedBlankPage = "Inserted blank page.";
    public const string Duplicating = "Duplicating…";
    public const string Cropping = "Cropping…";
    public const string NoPagesToInsert = "No pages to insert.";
    public const string UndidPageEdit = "Undid page edit.";
    public const string RedidPageEdit = "Redid page edit.";
    public const string SplitCancelled = "Split cancelled.";
    public const string SplitWouldBeSingle = "Split would produce a single document.";
    public const string PastePagesFailedPrefix = "Paste pages failed: ";

    public static string FormatPastePagesFailed(string message) =>
        PastePagesFailedPrefix + message;

    public static string FormatMerging(int fileCount) =>
        fileCount == 1 ? "Merging PDF…" : $"Merging {fileCount} PDFs…";

    public static string FormatMerged(int pagesAdded) =>
        pagesAdded == 1 ? "Merged 1 page." : $"Merged {pagesAdded} pages.";

    public static string FormatSplitting(int partCount) =>
        $"Splitting into {partCount} PDFs…";

    public static string FormatSplitDone(int partCount, string folderName) =>
        $"Split into {partCount} PDFs in {folderName}.";
}
