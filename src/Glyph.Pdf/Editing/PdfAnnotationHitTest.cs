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
}
