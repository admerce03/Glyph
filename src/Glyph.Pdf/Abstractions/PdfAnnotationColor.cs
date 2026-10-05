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

    public static PdfAnnotationColor InkRed { get; } = new(220, 60, 40);

    public static PdfAnnotationColor StrokeBlue { get; } = new(30, 144, 255);

    public static PdfAnnotationColor StrokeGreen { get; } = new(40, 160, 60);

    public static PdfAnnotationColor StrokeOrange { get; } = new(255, 140, 0);

    public static PdfAnnotationColor StrokePurple { get; } = new(140, 60, 200);

    public static PdfAnnotationColor StrokeBlack { get; } = new(20, 20, 20);

    /// <summary>Stroke/border colors for ink and shape tools.</summary>
    public static IReadOnlyList<(string Name, PdfAnnotationColor Color)> StrokePresets { get; } =
    [
        ("Red", InkRed),
        ("Dodger blue", StrokeBlue),
        ("Green", StrokeGreen),
        ("Orange", StrokeOrange),
        ("Purple", StrokePurple),
        ("Black", StrokeBlack),
    ];
}
