namespace Glyph.Core.Documents;

/// <summary>
/// Page pairing helpers for single/two-page layout modes.
/// TwoPage pairs 0-1, 2-3, …
/// TwoPageWithCover shows page 0 alone, then 1-2, 3-4, …
/// </summary>
public static class PageLayoutCalculator
{
    public static (int StartInclusive, int EndInclusive) VisibleRange(
        PageLayoutMode mode,
        int currentPageIndex,
        int pageCount)
    {
        if (pageCount <= 0)
        {
            return (0, -1);
        }

        var index = Math.Clamp(currentPageIndex, 0, pageCount - 1);
        return mode switch
        {
            PageLayoutMode.SinglePage => (index, index),
            PageLayoutMode.TwoPage => TwoPageRange(index, pageCount, coverFirst: false),
            PageLayoutMode.TwoPageWithCover => TwoPageRange(index, pageCount, coverFirst: true),
            _ => (0, pageCount - 1),
        };
    }

    public static int NormalizePageIndex(PageLayoutMode mode, int requestedIndex, int pageCount)
    {
        if (pageCount <= 0)
        {
            return 0;
        }

        var index = Math.Clamp(requestedIndex, 0, pageCount - 1);
        return mode switch
        {
            PageLayoutMode.TwoPage when index % 2 == 1 => index - 1,
            PageLayoutMode.TwoPageWithCover when index > 0 && index % 2 == 0 => index - 1,
            _ => index,
        };
    }

    public static int NextPageIndex(PageLayoutMode mode, int currentPageIndex, int pageCount)
    {
        if (pageCount <= 0)
        {
            return 0;
        }

        var step = IsFacingMode(mode) ? FacingStep(mode, currentPageIndex, forward: true) : 1;
        return Math.Min(pageCount - 1, currentPageIndex + step);
    }

    public static int PreviousPageIndex(PageLayoutMode mode, int currentPageIndex, int pageCount)
    {
        if (pageCount <= 0)
        {
            return 0;
        }

        var step = IsFacingMode(mode) ? FacingStep(mode, currentPageIndex, forward: false) : 1;
        return Math.Max(0, currentPageIndex - step);
    }

    public static int FirstPageIndex(PageLayoutMode mode, int pageCount) =>
        pageCount <= 0 ? 0 : NormalizePageIndex(mode, 0, pageCount);

    public static int LastPageIndex(PageLayoutMode mode, int pageCount) =>
        pageCount <= 0 ? 0 : NormalizePageIndex(mode, pageCount - 1, pageCount);

    private static bool IsFacingMode(PageLayoutMode mode) =>
        mode is PageLayoutMode.TwoPage or PageLayoutMode.TwoPageWithCover;

    private static int FacingStep(PageLayoutMode mode, int currentPageIndex, bool forward)
    {
        if (mode == PageLayoutMode.TwoPageWithCover)
        {
            if (forward)
            {
                return currentPageIndex == 0 ? 1 : 2;
            }

            return currentPageIndex <= 1 ? currentPageIndex : 2;
        }

        return 2;
    }

    private static (int StartInclusive, int EndInclusive) TwoPageRange(int index, int pageCount, bool coverFirst)
    {
        if (coverFirst)
        {
            if (index <= 0)
            {
                return (0, 0);
            }

            var left = index % 2 == 1 ? index : index - 1;
            var right = Math.Min(pageCount - 1, left + 1);
            return (left, right);
        }

        var evenLeft = index % 2 == 0 ? index : index - 1;
        var evenRight = Math.Min(pageCount - 1, evenLeft + 1);
        return (evenLeft, evenRight);
    }
}
