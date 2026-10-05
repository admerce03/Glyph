namespace Glyph.Pdf.Text;

/// <summary>
/// Next/previous match wrapping and status text for Find results (F06-13/14).
/// </summary>
public static class PdfSearchHitNav
{
    /// <summary>
    /// Wraps <paramref name="hitIndex"/> into [0, hitCount). Returns -1 when there are no hits.
    /// </summary>
    public static int WrapIndex(int hitIndex, int hitCount)
    {
        if (hitCount <= 0)
        {
            return -1;
        }

        return (hitIndex % hitCount + hitCount) % hitCount;
    }

    public static string FormatStatus(int zeroBasedIndex, int hitCount, int pageIndex) =>
        $"Match {zeroBasedIndex + 1} / {hitCount} · p.{pageIndex + 1}";
}
