namespace Glyph.Core.Documents;

/// <summary>
/// Computes which continuous-scroll page placeholders to materialize around the
/// current page so large documents do not build a visual for every page.
/// </summary>
public static class ContinuousPageWindow
{
    public const int DefaultRadius = 4;

    public static (int StartInclusive, int EndInclusive) Around(
        int currentPageIndex,
        int pageCount,
        int radius = DefaultRadius)
    {
        if (pageCount <= 0)
        {
            return (0, -1);
        }

        var index = Math.Clamp(currentPageIndex, 0, pageCount - 1);
        var start = Math.Max(0, index - Math.Max(0, radius));
        var end = Math.Min(pageCount - 1, index + Math.Max(0, radius));
        return (start, end);
    }
}
