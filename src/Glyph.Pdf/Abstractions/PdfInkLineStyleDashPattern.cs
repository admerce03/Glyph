namespace Glyph.Pdf.Abstractions;

/// <summary>
/// WinUI/XAML-compatible dash arrays for ink/line preview strokes (F13 ink styles).
/// </summary>
public static class PdfInkLineStyleDashPattern
{
    /// <summary>
    /// Alternating dash/gap lengths in DIP units, or null for solid lines.
    /// </summary>
    public static IReadOnlyList<double>? ForPreview(PdfInkLineStyle style) =>
        style switch
        {
            PdfInkLineStyle.Dashed => [6, 4],
            PdfInkLineStyle.Dotted => [1.5, 4],
            _ => null,
        };
}
