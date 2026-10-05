namespace Glyph.Core.Ocr;

/// <summary>
/// OCR result dialog labels and completion status (F08-03/04).
/// </summary>
public static class OcrResultDialog
{
    public const string CopyButton = "Copy text";
    public const string TextCopied = "OCR text copied.";
    public const string Cancelled = "OCR cancelled.";

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

    public static string PageSectionHeader(int pageIndex0Based, string body) =>
        string.IsNullOrWhiteSpace(body)
            ? body
            : $"--- Page {pageIndex0Based + 1} ---\n{body}";
}
