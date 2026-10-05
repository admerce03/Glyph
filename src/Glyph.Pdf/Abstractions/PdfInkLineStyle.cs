namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Stroke pattern for ink-based lines and arrows (PDFium has no Line/BS dash API).
/// </summary>
public enum PdfInkLineStyle
{
    Solid = 0,
    Dashed = 1,
    Dotted = 2,
}
