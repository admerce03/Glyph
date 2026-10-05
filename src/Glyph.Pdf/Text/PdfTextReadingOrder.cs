using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Text;

/// <summary>
/// Line/word break detection when joining PDF text chars (F07-03 reading order).
/// </summary>
public static class PdfTextReadingOrder
{
    public static double MidY(PdfRect bounds) => (bounds.Top + bounds.Bottom) / 2;

    public static bool IsNewLine(PdfRect previous, PdfRect current)
    {
        var prevMid = MidY(previous);
        var currMid = MidY(current);
        var lineHeight = Math.Max(previous.Height, current.Height);
        var threshold = Math.Max(2.0, lineHeight * 0.45);
        return Math.Abs(prevMid - currMid) > threshold;
    }

    public static bool NeedsSpaceBetween(PdfTextChar previous, PdfTextChar current)
    {
        if (string.IsNullOrWhiteSpace(previous.Value) || string.IsNullOrWhiteSpace(current.Value))
        {
            return false;
        }

        if (char.IsWhiteSpace(previous.Value[^1]) || char.IsWhiteSpace(current.Value[0]))
        {
            return false;
        }

        var gap = current.Bounds.Left - previous.Bounds.Right;
        var typical = Math.Max(previous.Bounds.Width, current.Bounds.Width);
        return gap > Math.Max(2.0, typical * 0.2);
    }
}
