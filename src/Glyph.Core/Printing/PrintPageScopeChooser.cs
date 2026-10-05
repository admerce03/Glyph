namespace Glyph.Core.Printing;

/// <summary>
/// Print dialog page-scope chooser labels (F44-01/02/04).
/// </summary>
public static class PrintPageScopeChooser
{
    public const string CurrentPage = "Current page";
    public const string SelectedPages = "Selected pages";
    public const string PageRange = "Page range…";
    public const string AllPages = "All pages";
    public const string CancelledStatus = "Print cancelled.";
    public const string NoPagesToPrint = "No pages to print.";
    public const string PrintingNotAllowed = "This PDF does not allow printing.";

    public static string FormatPreparing(int pageCount) =>
        $"Preparing {pageCount} page(s) for print…";

    public static string FormatPrintUiShown(int pageCount, int pagesPerSheet) =>
        $"Print UI shown · {pageCount} page(s)"
        + (pagesPerSheet > 1 ? $" · {pagesPerSheet}-up." : ".");

    public static IReadOnlyList<string> Labels { get; } =
    [
        CurrentPage,
        SelectedPages,
        PageRange,
        AllPages,
    ];

    public enum Scope
    {
        CurrentPage = 0,
        SelectedPages = 1,
        PageRange = 2,
        AllPages = 3,
    }

    public static Scope FromComboIndex(int selectedIndex) =>
        (Scope)Math.Clamp(selectedIndex, 0, Labels.Count - 1);
}
