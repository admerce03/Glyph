namespace Glyph.Core.Documents;

/// <summary>
/// Full-document background text indexing for Find (F57-05 / F58-06).
/// When enabled, <c>IPdfTextSearchService.WarmIndexAsync</c> prefetches page text
/// so subsequent searches avoid re-opening the PDF.
/// </summary>
public static class BackgroundSearchIndexPolicy
{
    public const bool BackgroundIndexingEnabled = true;

    /// <summary>Find still accepts queries at any time; warm is best-effort.</summary>
    public const bool SearchIsOnDemand = true;

    public const string Reason =
        "Background page-text warm via PdfPigTextSearchService.WarmIndexAsync; Find remains available immediately.";
}
