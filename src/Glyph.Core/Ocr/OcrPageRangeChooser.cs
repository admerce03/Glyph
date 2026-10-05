namespace Glyph.Core.Ocr;

/// <summary>
/// OCR scope chooser labels and page lists (F08-06/07/08).
/// </summary>
public static class OcrPageRangeChooser
{
    public static string SelectedLabel(IReadOnlyList<int> selectedPages) =>
        selectedPages.Count == 1
            ? $"Selected / current (page {selectedPages[0] + 1})"
            : $"Selected pages ({selectedPages.Count})";

    public static string EntireDocumentLabel(int pageCount) =>
        $"Entire document ({pageCount} pages)";

    public static string PrimaryButton(int selectedCount) =>
        selectedCount == 1 ? "Current page" : "Selected pages";

    public const string SecondaryButton = "Entire document";

    public static IReadOnlyList<int> EntireDocumentPages(int pageCount) =>
        pageCount <= 0 ? [] : Enumerable.Range(0, pageCount).ToArray();

    public static string ProgressStatus(int pageIndex0Based, int ordinal1Based, int total) =>
        total == 1
            ? $"Running OCR on page {pageIndex0Based + 1}… (1/1)"
            : $"Running OCR on page {pageIndex0Based + 1} ({ordinal1Based}/{total})…";
}
