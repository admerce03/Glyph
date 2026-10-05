using Glyph.Pdf.Abstractions;
using System.Text;

namespace Glyph.Pdf.Text;

/// <summary>
/// Pure helpers for selecting PDF text characters by geometry or index range,
/// preserving visual reading order across lines.
/// </summary>
public static class PdfTextSelection
{
    public static string CopyText(IReadOnlyList<PdfTextChar> chars, int startIndex, int endIndexInclusive)
    {
        if (chars.Count == 0)
        {
            return string.Empty;
        }

        var start = Math.Clamp(Math.Min(startIndex, endIndexInclusive), 0, chars.Count - 1);
        var end = Math.Clamp(Math.Max(startIndex, endIndexInclusive), 0, chars.Count - 1);
        var slice = chars.Skip(start).Take(end - start + 1).ToList();
        return JoinInReadingOrder(slice);
    }

    public static IReadOnlyList<PdfTextChar> CharsInRect(IReadOnlyList<PdfTextChar> chars, PdfRect selection)
    {
        return OrderForReading(chars.Where(c => c.Bounds.Intersects(selection))).ToList();
    }

    public static string CopyCharsInRect(IReadOnlyList<PdfTextChar> chars, PdfRect selection)
        => JoinInReadingOrder(CharsInRect(chars, selection));

    /// <summary>
    /// Select / copy every character on a page in visual reading order.
    /// </summary>
    public static string CopyAll(IReadOnlyList<PdfTextChar> chars)
        => JoinInReadingOrder(chars);

    /// <summary>
    /// Stream-style selection from the character nearest <paramref name="start"/> to the
    /// character nearest <paramref name="end"/> (inclusive), spanning multiple lines.
    /// </summary>
    public static string CopyAcrossLines(
        IReadOnlyList<PdfTextChar> chars,
        double startX,
        double startY,
        double endX,
        double endY)
    {
        if (chars.Count == 0)
        {
            return string.Empty;
        }

        var startIndex = NearestCharIndex(chars, startX, startY);
        var endIndex = NearestCharIndex(chars, endX, endY);
        return CopyText(chars, startIndex, endIndex);
    }

    public static IEnumerable<PdfTextChar> OrderForReading(IEnumerable<PdfTextChar> chars)
        => chars
            .OrderByDescending(c => PdfTextReadingOrder.MidY(c.Bounds))
            .ThenBy(c => c.Bounds.Left)
            .ThenBy(c => c.Index);

    public static string JoinInReadingOrder(IReadOnlyList<PdfTextChar> chars)
    {
        if (chars.Count == 0)
        {
            return string.Empty;
        }

        var ordered = OrderForReading(chars).ToList();
        var sb = new StringBuilder();
        PdfTextChar? prev = null;
        foreach (var current in ordered)
        {
            if (prev is not null)
            {
                if (PdfTextReadingOrder.IsNewLine(prev.Bounds, current.Bounds))
                {
                    sb.Append('\n');
                }
                else if (PdfTextReadingOrder.NeedsSpaceBetween(prev, current))
                {
                    sb.Append(' ');
                }
            }

            sb.Append(current.Value);
            prev = current;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Column/region selection when Alt is held, or the drag is wide and short.
    /// </summary>
    public static bool PreferColumnMode(bool altHeld, double rectWidth, double rectHeight) =>
        altHeld || (rectWidth > Math.Max(40, rectHeight * 1.75) && rectHeight > 18);

    public static int NearestCharIndex(IReadOnlyList<PdfTextChar> chars, double x, double y)
    {
        var best = 0;
        var bestDist = double.MaxValue;
        for (var i = 0; i < chars.Count; i++)
        {
            var b = chars[i].Bounds;
            var cx = (b.Left + b.Right) / 2;
            var cy = MidY(b);
            var dist = Math.Abs(cx - x) + Math.Abs(cy - y);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// Click word-ish selection: pick the glyph nearest <paramref name="x"/>/<paramref name="y"/>
    /// using Left/Bottom Manhattan distance, then expand over contiguous non-whitespace runs.
    /// </summary>
    public static bool TryExpandWordAt(
        IReadOnlyList<PdfTextChar> chars,
        double x,
        double y,
        out int startIndex,
        out int endIndexInclusive)
    {
        startIndex = 0;
        endIndexInclusive = -1;
        if (chars.Count == 0)
        {
            return false;
        }

        var best = 0;
        var bestDist = double.MaxValue;
        for (var i = 0; i < chars.Count; i++)
        {
            var b = chars[i].Bounds;
            var dist = Math.Abs(b.Left - x) + Math.Abs(b.Bottom - y);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        var start = best;
        var end = best;
        if (char.IsWhiteSpace(chars[best].Value.FirstOrDefault()))
        {
            startIndex = start;
            endIndexInclusive = end;
            return true;
        }

        while (start > 0 && !char.IsWhiteSpace(chars[start - 1].Value.FirstOrDefault()))
        {
            start--;
        }

        while (end + 1 < chars.Count && !char.IsWhiteSpace(chars[end + 1].Value.FirstOrDefault()))
        {
            end++;
        }

        startIndex = start;
        endIndexInclusive = end;
        return true;
    }

}
