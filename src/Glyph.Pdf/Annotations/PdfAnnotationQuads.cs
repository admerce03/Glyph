using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Annotations;

/// <summary>
/// Builds PDF QuadPoints from character bounding boxes for text markup annotations.
/// </summary>
public static class PdfAnnotationQuads
{
    /// <summary>
    /// Groups selected characters into per-line quads (union of char boxes on the same baseline band).
    /// </summary>
    public static IReadOnlyList<PdfQuad> FromChars(IEnumerable<PdfTextChar> chars)
    {
        var list = chars.ToList();
        if (list.Count == 0)
        {
            return [];
        }

        // Sort reading order, then cluster by vertical overlap of centers.
        var ordered = list
            .OrderByDescending(c => (c.Bounds.Top + c.Bounds.Bottom) / 2)
            .ThenBy(c => c.Bounds.Left)
            .ToList();

        var lines = new List<List<PdfTextChar>>();
        foreach (var ch in ordered)
        {
            var centerY = (ch.Bounds.Top + ch.Bounds.Bottom) / 2;
            var line = lines.FirstOrDefault(l =>
            {
                var sample = l[0].Bounds;
                var sampleCenter = (sample.Top + sample.Bottom) / 2;
                var band = Math.Max(2, Math.Max(sample.Height, ch.Bounds.Height) * 0.6);
                return Math.Abs(sampleCenter - centerY) <= band;
            });

            if (line is null)
            {
                lines.Add([ch]);
            }
            else
            {
                line.Add(ch);
            }
        }

        return lines.Select(LineToQuad).ToList();
    }

    public static PdfQuad FromRect(PdfRect rect) =>
        new(
            rect.Left,
            rect.Top,
            rect.Right,
            rect.Top,
            rect.Left,
            rect.Bottom,
            rect.Right,
            rect.Bottom);

    public static PdfRect BoundsFromQuads(IReadOnlyList<PdfQuad> quads)
    {
        if (quads.Count == 0)
        {
            return default;
        }

        var left = quads.Min(q => Math.Min(Math.Min(q.X1, q.X2), Math.Min(q.X3, q.X4)));
        var right = quads.Max(q => Math.Max(Math.Max(q.X1, q.X2), Math.Max(q.X3, q.X4)));
        var bottom = quads.Min(q => Math.Min(Math.Min(q.Y1, q.Y2), Math.Min(q.Y3, q.Y4)));
        var top = quads.Max(q => Math.Max(Math.Max(q.Y1, q.Y2), Math.Max(q.Y3, q.Y4)));
        return new PdfRect(left, bottom, right, top);
    }

    private static PdfQuad LineToQuad(List<PdfTextChar> line)
    {
        var left = line.Min(c => c.Bounds.Left);
        var right = line.Max(c => c.Bounds.Right);
        var bottom = line.Min(c => c.Bounds.Bottom);
        var top = line.Max(c => c.Bounds.Top);
        return FromRect(new PdfRect(left, bottom, right, top));
    }
}
