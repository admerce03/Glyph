namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Default semi-transparent fill derived from stroke hue (F17-19).
/// </summary>
public static class PdfShapeFillPolicy
{
    public const byte SemiTransparentAlpha = 40;

    public static bool UsesFill(PdfShapeKind kind) =>
        kind is not (PdfShapeKind.Line
            or PdfShapeKind.Arrow
            or PdfShapeKind.Star
            or PdfShapeKind.SpeechBubble
            or PdfShapeKind.HighlightRectangle
            or PdfShapeKind.Loupe);

    public static PdfAnnotationColor? FromStroke(PdfShapeKind kind, PdfAnnotationColor stroke) =>
        UsesFill(kind)
            ? new PdfAnnotationColor(stroke.R, stroke.G, stroke.B, SemiTransparentAlpha)
            : null;
}
