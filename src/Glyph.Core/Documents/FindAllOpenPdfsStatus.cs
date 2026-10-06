namespace Glyph.Core.Documents;

/// <summary>
/// Status strings for Edit → Find in all open PDFs (F06-08).
/// </summary>
public static class FindAllOpenPdfsStatus
{
    public const string DialogTitle = "Find in all open PDFs";
    public const string SearchButton = "Search";
    public const string QueryPlaceholder = "Search all open PDFs";
    public const string AnyWordLabel = "Any word";
    public const string AnyWordTooltip = "Match any word (not exact phrase)";
    public const string MatchCaseLabel = "Match case";
    public const string MatchCaseTooltip = "Case-sensitive search across open PDFs";
    public const string NoDocuments = "No open PDF documents to search.";
    public const string EmptyQuery = "Enter search text.";
    public const string Cancelled = "Search cancelled.";
    public const string NoMatches = "No matches in open PDFs.";

    public static string Searching(int pdfCount) => $"Searching {pdfCount} PDF(s)…";

    public static string MatchCount(int hitCount) => $"{hitCount} match(es) across open PDFs.";

    public static string OpenedHit(string displayName, int pageIndex0Based) =>
        $"Opened {displayName} p.{pageIndex0Based + 1}.";
}
