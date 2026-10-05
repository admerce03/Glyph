namespace Glyph.Core.Text;

/// <summary>
/// Ctrl+A text selection escalation: page → document (F07-05).
/// </summary>
public static class PdfTextSelectAllPolicy
{
    /// <summary>
    /// True when a second Ctrl+A should expand from a full-page selection to the whole document.
    /// </summary>
    public static bool ShouldExpandToDocument(
        int selectionPageIndex,
        int currentPageIndex,
        string? selectedText,
        string? fullPageText,
        int documentPageCount) =>
        selectionPageIndex == currentPageIndex
        && documentPageCount > 1
        && !string.IsNullOrEmpty(selectedText)
        && !string.IsNullOrEmpty(fullPageText)
        && string.Equals(selectedText, fullPageText, StringComparison.Ordinal);

    public static string JoinDocumentParts(IEnumerable<string> pageTexts) =>
        string.Join("\n\n", pageTexts.Where(static t => !string.IsNullOrWhiteSpace(t)));

    public static string DocumentStatus(int pageParts, int characterCount) =>
        $"Selected all text in document ({pageParts} page(s), {characterCount} characters).";

    public static string PageStatus(int pageIndex0Based, int characterCount) =>
        $"Selected all text on page {pageIndex0Based + 1} ({characterCount} characters).";
}
