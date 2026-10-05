namespace Glyph.Core.Documents;

/// <summary>
/// Insert-before indices for PDF page operations (F10-11/12 append/prepend).
/// </summary>
public static class PageInsertIndex
{
    public const int Prepend = 0;

    public static int Append(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        return pageCount;
    }

    public static int Clamp(int insertBefore, int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        return Math.Clamp(insertBefore, 0, pageCount);
    }
}
