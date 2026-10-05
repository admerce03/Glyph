namespace Glyph.Pdf.Abstractions;

/// <summary>
/// 8-bit RGBA color for PDF annotations (A=255 is opaque).
/// </summary>
public readonly record struct PdfAnnotationColor(byte R, byte G, byte B, byte A = 255)
{
    public static PdfAnnotationColor YellowHighlight { get; } = new(255, 230, 0, 128);

    public static PdfAnnotationColor UnderlineBlue { get; } = new(30, 144, 255, 255);

    public static PdfAnnotationColor StrikeOutRed { get; } = new(220, 50, 50, 255);
}
