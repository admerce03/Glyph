using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Text;

/// <summary>
/// Builds PDF QuadPoints for text-markup annotations from character bounds.
/// Contiguous characters that share a baseline band are merged into one quad.
/// </summary>
public static class PdfTextMarkupQuads
{
    public static IReadOnlyList<PdfQuad> FromChars(IReadOnlyList<PdfTextChar> chars)
    {
        if (chars.Count == 0)
        {
            return [];
        }

        var ordered = chars.OrderBy(c => c.Index).ToList();
        var quads = new List<PdfQuad>();
        var run = new List<PdfTextChar> { ordered[0] };

        for (var i = 1; i < ordered.Count; i++)
        {
            var prev = run[^1];
            var cur = ordered[i];
            var sameLine = Math.Abs(prev.Bounds.Bottom - cur.Bounds.Bottom) <= Math.Max(2.0, prev.Bounds.Height * 0.35)
                && Math.Abs(prev.Bounds.Top - cur.Bounds.Top) <= Math.Max(2.0, prev.Bounds.Height * 0.35);
            var contiguous = cur.Bounds.Left <= prev.Bounds.Right + Math.Max(4.0, prev.Bounds.Width);

            if (sameLine && contiguous)
            {
                run.Add(cur);
                continue;
            }

            quads.Add(UnionQuad(run));
            run.Clear();
            run.Add(cur);
        }

        quads.Add(UnionQuad(run));
        return quads;
    }

    public static IReadOnlyList<PdfQuad> FromSelectionRect(IReadOnlyList<PdfTextChar> chars, PdfRect selection) =>
        FromChars(PdfTextSelection.CharsInRect(chars, selection));

    public static IReadOnlyList<PdfQuad> FromIndexRange(IReadOnlyList<PdfTextChar> chars, int startIndex, int endIndexInclusive)
    {
        if (chars.Count == 0)
        {
            return [];
        }

        var start = Math.Clamp(Math.Min(startIndex, endIndexInclusive), 0, chars.Count - 1);
        var end = Math.Clamp(Math.Max(startIndex, endIndexInclusive), 0, chars.Count - 1);
        return FromChars(chars.Skip(start).Take(end - start + 1).ToList());
    }

    private static PdfQuad UnionQuad(IReadOnlyList<PdfTextChar> run)
    {
        var union = run[0].Bounds;
        for (var i = 1; i < run.Count; i++)
        {
            var b = run[i].Bounds;
            union = new PdfRect(
                Math.Min(union.Left, b.Left),
                Math.Min(union.Bottom, b.Bottom),
                Math.Max(union.Right, b.Right),
                Math.Max(union.Top, b.Top));
        }

        return PdfQuad.FromRect(union);
    }
}
