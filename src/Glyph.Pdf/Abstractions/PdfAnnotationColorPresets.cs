namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Named annotation fill/stroke color presets for viewer dialogs.
/// </summary>
public static class PdfAnnotationColorPresets
{
    public const string NoneClear = "None (clear)";
    public const string White = "White";
    public const string Yellow = "Yellow";
    public const string LightBlue = "Light blue";
    public const string LightGreen = "Light green";
    public const string TranslucentYellow = "Translucent yellow";
    public const string DodgerBlue = "Dodger blue";
    public const string Red = "Red";
    public const string Green = "Green";
    public const string Orange = "Orange";
    public const string Purple = "Purple";

    public static IReadOnlyList<(string Name, PdfAnnotationColor? Color)> FillChoices { get; } =
    [
        (NoneClear, null),
        (White, new PdfAnnotationColor(255, 255, 255)),
        (Yellow, new PdfAnnotationColor(255, 250, 180)),
        (LightBlue, new PdfAnnotationColor(200, 230, 255)),
        (LightGreen, new PdfAnnotationColor(210, 245, 210)),
        (TranslucentYellow, new PdfAnnotationColor(255, 230, 80, 70)),
    ];

    public static IReadOnlyList<(string Name, PdfAnnotationColor Color)> StrokePresets { get; } =
    [
        (DodgerBlue, new PdfAnnotationColor(30, 144, 255)),
        (Red, new PdfAnnotationColor(220, 50, 50)),
        (Green, new PdfAnnotationColor(40, 160, 60)),
        (Orange, new PdfAnnotationColor(255, 140, 0)),
        (Purple, new PdfAnnotationColor(140, 60, 200)),
    ];

    public static string FormatFillTitle(string annotationLabel) =>
        $"Fill — {annotationLabel}";
}
