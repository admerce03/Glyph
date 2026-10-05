namespace Glyph.Core.Documents;

/// <summary>
/// Document performance / virtualization posture (F57 / F58-08).
/// </summary>
public static class PerformanceBehaviorPolicy
{
    /// <summary>Visible page renders before off-screen thumbnails.</summary>
    public const bool PreferVisiblePageBeforeThumbs = true;

    /// <summary>OCR starts only from explicit user action.</summary>
    public const bool LazyOcrRequiresExplicitRequest = true;

    public const string CancelOcrButton = "Cancel OCR";

    public static bool IsOutsideMaterializedWindow(
        int pageIndex,
        int currentPageIndex,
        int pageCount,
        int radius = ContinuousPageWindow.DefaultRadius)
    {
        var (start, end) = ContinuousPageWindow.Around(currentPageIndex, pageCount, radius);
        return pageIndex < start || pageIndex > end;
    }

    public static IReadOnlyList<string> ProgressSurfaces { get; } =
    [
        "OCR",
        "Export",
        "Optimize",
        "Batch images",
    ];
}
