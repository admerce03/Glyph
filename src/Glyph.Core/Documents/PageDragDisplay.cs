namespace Glyph.Core.Documents;

/// <summary>
/// User-visible strings for page drag/copy operations (F11 / F10-24).
/// </summary>
public static class PageDragDisplay
{
    public static string DragTitle(int pageCount) =>
        pageCount == 1 ? "PDF page" : $"{pageCount} PDF pages";

    public static string CopiedPagesStatus(int pageCount) =>
        pageCount == 1 ? "Copied 1 page." : $"Copied {pageCount} pages.";

    public static string PastingPagesStatus(int pageCount) =>
        pageCount == 1 ? "Pasting page…" : $"Pasting {pageCount} pages…";

    public static string PastedPagesStatus(int pageCount) =>
        pageCount == 1 ? "Pasted 1 page." : $"Pasted {pageCount} pages.";
}
