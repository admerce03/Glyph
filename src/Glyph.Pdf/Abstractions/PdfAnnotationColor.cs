namespace Glyph.Pdf.Abstractions;

/// <summary>
/// 8-bit RGBA color for PDF annotations (A=255 is opaque).
/// </summary>
public readonly record struct PdfAnnotationColor(byte R, byte G, byte B, byte A = 255)
{
    public static PdfAnnotationColor YellowHighlight { get; } = new(255, 230, 0, 128);

    public static PdfAnnotationColor GreenHighlight { get; } = new(120, 220, 80, 128);

    public static PdfAnnotationColor PinkHighlight { get; } = new(255, 120, 180, 128);

    public static PdfAnnotationColor BlueHighlight { get; } = new(80, 160, 255, 128);

    public static PdfAnnotationColor OrangeHighlight { get; } = new(255, 160, 40, 128);

    /// <summary>Preset highlight colors for the markup toolbar.</summary>
    public static IReadOnlyList<(string Name, PdfAnnotationColor Color)> HighlightPresets { get; } =
    [
        ("Yellow", YellowHighlight),
        ("Green", GreenHighlight),
        ("Pink", PinkHighlight),
        ("Blue", BlueHighlight),
        ("Orange", OrangeHighlight),
    ];

    public static PdfAnnotationColor UnderlineBlue { get; } = new(30, 144, 255, 255);

    public static PdfAnnotationColor StrikeOutRed { get; } = new(220, 50, 50, 255);

    public static PdfAnnotationColor StickyNoteYellow { get; } = new(255, 220, 80, 255);
}
