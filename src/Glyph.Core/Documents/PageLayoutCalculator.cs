namespace Glyph.Core.Documents;

/// <summary>
/// Page pairing helpers for single/two-page layout modes.
/// Two-page mode treats even indices as the left page of a spread (no separate cover mode yet).
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
            PageLayoutMode.TwoPage => TwoPageRange(index, pageCount),
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
        if (mode == PageLayoutMode.TwoPage && index % 2 == 1)
        {
            // Snap to the left page of the spread.
            return index - 1;
        }

        return index;
    }

    public static int NextPageIndex(PageLayoutMode mode, int currentPageIndex, int pageCount)
    {
        if (pageCount <= 0)
        {
            return 0;
        }

        var step = mode == PageLayoutMode.TwoPage ? 2 : 1;
        return Math.Min(pageCount - 1, currentPageIndex + step);
    }

    public static int PreviousPageIndex(PageLayoutMode mode, int currentPageIndex, int pageCount)
    {
        if (pageCount <= 0)
        {
            return 0;
        }

        var step = mode == PageLayoutMode.TwoPage ? 2 : 1;
        return Math.Max(0, currentPageIndex - step);
    }

    private static (int StartInclusive, int EndInclusive) TwoPageRange(int index, int pageCount)
    {
        var left = index % 2 == 0 ? index : index - 1;
        var right = Math.Min(pageCount - 1, left + 1);
        return (left, right);
    }
}
