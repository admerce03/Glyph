namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Opaque RGBA color for PDF annotations (each channel 0–255).
/// </summary>
public readonly record struct PdfAnnotationColor(byte R, byte G, byte B, byte A = 255)
{
    public static PdfAnnotationColor Yellow => new(255, 230, 80, 180);

    public static PdfAnnotationColor Green => new(120, 220, 120, 180);

    public static PdfAnnotationColor Pink => new(255, 140, 180, 180);

    public static PdfAnnotationColor Blue => new(120, 180, 255, 180);

    public static PdfAnnotationColor Red => new(220, 60, 60, 220);

    public static PdfAnnotationColor Black => new(20, 20, 20, 255);
}
