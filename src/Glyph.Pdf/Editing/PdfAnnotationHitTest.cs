using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Editing;

/// <summary>
/// Hit-test helpers for selecting annotations by page-space point.
/// </summary>
public static class PdfAnnotationHitTest
{
    public static PdfAnnotationInfo? HitTest(
        IReadOnlyList<PdfAnnotationInfo> annotations,
        int pageIndex,
        double xPoints,
        double yPoints)
    {
        ArgumentNullException.ThrowIfNull(annotations);

        // Prefer top-most (highest annot index) on the page.
        PdfAnnotationInfo? hit = null;
        foreach (var annot in annotations)
        {
            if (annot.PageIndex != pageIndex)
            {
                continue;
            }

            if (annot.Bounds.ContainsPoint(xPoints, yPoints))
            {
                if (hit is null || annot.AnnotIndex >= hit.AnnotIndex)
                {
                    hit = annot;
                }
            }
        }

        return hit;
    }

    /// <summary>
    /// Eraser-style hit test: expands each annotation's bounds by <paramref name="pad"/>
    /// (handles inverted rects) and prefers the highest <see cref="PdfAnnotationInfo.AnnotIndex"/>.
    /// </summary>
    public static PdfAnnotationInfo? HitTestWithPad(
        IEnumerable<PdfAnnotationInfo> annotations,
        double xPoints,
        double yPoints,
        double pad)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        if (pad < 0)
        {
            pad = 0;
        }

        PdfAnnotationInfo? hit = null;
        foreach (var annot in annotations)
        {
            var b = annot.Bounds;
            var left = Math.Min(b.Left, b.Right) - pad;
            var right = Math.Max(b.Left, b.Right) + pad;
            var bottom = Math.Min(b.Bottom, b.Top) - pad;
            var top = Math.Max(b.Bottom, b.Top) + pad;
            if (xPoints < left || xPoints > right || yPoints < bottom || yPoints > top)
            {
                continue;
            }

            if (hit is null || annot.AnnotIndex >= hit.AnnotIndex)
            {
                hit = annot;
            }
        }

        return hit;
    }
}
