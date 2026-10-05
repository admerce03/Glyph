namespace Glyph.Core.Ocr;

/// <summary>
/// OCR result dialog labels and completion status (F08-03/04).
/// </summary>
public static class OcrResultDialog
{
    public const string CopyButton = "Copy text";
    public const string TextCopied = "OCR text copied.";
    public const string Cancelled = "OCR cancelled.";
    public const string Cancelling = "Cancelling OCR…";
    public const string EngineUnavailable = "OCR engine unavailable.";
    public const string NoPagesSelected = "No pages selected for OCR.";
    public const string OverlaysCleared = "OCR overlays cleared.";
    public const string SearchableExportNeedsOcr = "Run OCR before exporting a searchable PDF.";
    public const string BuildingSearchablePdf = "Building searchable OCR PDF…";
    public const string SearchablePdfCancelled = "OCR→PDF cancelled.";
    public const string SearchablePdfFailedPrefix = "OCR→PDF failed: ";

    public static string Title(int pageCount) =>
        pageCount == 1 ? "OCR result" : "OCR results";

    public static string Summary(int pageCount, int firstPageIndex0Based, int totalLines, int totalWords) =>
        pageCount == 1
            ? $"Page {firstPageIndex0Based + 1} · {totalLines} line(s) · {totalWords} word(s)"
            : $"{pageCount} pages · {totalLines} line(s) · {totalWords} word(s)";

    public static string CompletionStatus(int pageCount, int firstPageIndex0Based, int totalLines) =>
        totalLines == 0
            ? (pageCount == 1
                ? $"OCR page {firstPageIndex0Based + 1} — no text."
                : $"OCR {pageCount} pages — no text.")
            : (pageCount == 1
                ? $"OCR page {firstPageIndex0Based + 1} — {totalLines} line(s). Click words to select."
                : $"OCR {pageCount} pages — {totalLines} line(s). Click words to select.");

    public static string Failed(string message) => "OCR failed: " + message;

    public static string SearchablePdfFailed(string message) =>
        SearchablePdfFailedPrefix + message;

    public static string SavedSearchablePdf(int pageCount, string fileName) =>
        $"Saved searchable OCR PDF ({pageCount} page(s)): {fileName}";

    public static string PageSectionHeader(int pageIndex0Based, string body) =>
        string.IsNullOrWhiteSpace(body)
            ? body
            : $"--- Page {pageIndex0Based + 1} ---\n{body}";
}
