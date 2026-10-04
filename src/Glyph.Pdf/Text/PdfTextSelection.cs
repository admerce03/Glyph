using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Text;

/// <summary>
/// Pure helpers for selecting PDF text characters by geometry or index range.
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
        return string.Concat(chars.Skip(start).Take(end - start + 1).Select(c => c.Value));
    }

    public static IReadOnlyList<PdfTextChar> CharsInRect(IReadOnlyList<PdfTextChar> chars, PdfRect selection)
    {
        return chars.Where(c => c.Bounds.Intersects(selection)).ToList();
    }

    public static string CopyCharsInRect(IReadOnlyList<PdfTextChar> chars, PdfRect selection)
    {
        var selected = CharsInRect(chars, selection);
        return string.Concat(selected.Select(c => c.Value));
    }
}
