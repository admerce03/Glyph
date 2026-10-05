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
            .OrderByDescending(c => MidY(c.Bounds))
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
                if (IsNewLine(prev.Bounds, current.Bounds))
                {
                    sb.Append('\n');
                }
                else if (NeedsSpace(prev, current))
                {
                    sb.Append(' ');
                }
            }

            sb.Append(current.Value);
            prev = current;
        }

        return sb.ToString();
    }

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

    private static double MidY(PdfRect bounds) => (bounds.Top + bounds.Bottom) / 2;

    private static bool IsNewLine(PdfRect previous, PdfRect current)
    {
        var prevMid = MidY(previous);
        var currMid = MidY(current);
        var lineHeight = Math.Max(previous.Height, current.Height);
        var threshold = Math.Max(2.0, lineHeight * 0.45);
        return Math.Abs(prevMid - currMid) > threshold;
    }

    private static bool NeedsSpace(PdfTextChar previous, PdfTextChar current)
    {
        if (string.IsNullOrWhiteSpace(previous.Value) || string.IsNullOrWhiteSpace(current.Value))
        {
            return false;
        }

        if (char.IsWhiteSpace(previous.Value[^1]) || char.IsWhiteSpace(current.Value[0]))
        {
            return false;
        }

        // Large horizontal gap on the same line usually means a word break.
        var gap = current.Bounds.Left - previous.Bounds.Right;
        var typical = Math.Max(previous.Bounds.Width, current.Bounds.Width);
        return gap > Math.Max(2.0, typical * 0.2);
    }
}
