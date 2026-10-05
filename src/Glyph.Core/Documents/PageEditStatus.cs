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
    public const string CropCancelled = "Crop cancelled.";
    public const string MergeCancelled = "Merge cancelled.";
    public const string PastePagesFailedPrefix = "Paste pages failed: ";

    public static string FormatPastePagesFailed(string message) =>
        PastePagesFailedPrefix + message;

    public static string FormatRotated(int count) =>
        $"Rotated {count} page{(count == 1 ? string.Empty : "s")}.";

    public static string FormatDeleted(int count) =>
        $"Deleted {count} page{(count == 1 ? string.Empty : "s")}.";

    public static string FormatDuplicated(int count) =>
        $"Duplicated {count} page{(count == 1 ? string.Empty : "s")}.";

    public static string FormatExtracted(int count, string fileName) =>
        $"Extracted {count} page{(count == 1 ? string.Empty : "s")} to {fileName}.";

    public static string FormatMerging(int fileCount) =>
        fileCount == 1 ? "Merging PDF…" : $"Merging {fileCount} PDFs…";

    public static string FormatMerged(int pagesAdded) =>
        pagesAdded == 1 ? "Merged 1 page." : $"Merged {pagesAdded} pages.";

    public static string FormatSplitting(int partCount) =>
        $"Splitting into {partCount} PDFs…";

    public static string FormatSplitDone(int partCount, string folderName) =>
        $"Split into {partCount} PDFs in {folderName}.";

    public static string FormatCropped(int count) =>
        count == 1 ? "Cropped 1 page." : $"Cropped {count} pages.";

    public static string FormatInserting(int count) =>
        count == 1 ? "Inserting page…" : $"Inserting {count} pages…";

    public static string FormatInserted(int count) =>
        count == 1 ? "Inserted 1 page." : $"Inserted {count} pages.";

    public static string FormatInsertedFromFile(int count, int fileCount) =>
        count == 1
            ? "Inserted 1 page from file."
            : $"Inserted {count} pages from file{(fileCount == 1 ? string.Empty : "s")}.";

    public static string FormatSelectedPages(int count) =>
        count == 1 ? "Selected 1 page." : $"Selected {count} pages.";
}
