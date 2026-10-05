namespace Glyph.Pdf.Abstractions;

/// <summary>
/// ComboBox / list option labels for PDF viewer dialogs and chrome.
/// </summary>
public static class PdfDialogOptions
{
    public static IReadOnlyList<string> LayoutModes { get; } =
    [
        "Continuous",
        "Single",
        "Two-page",
        "Two-page + cover",
        "Contact sheet",
    ];

    public static IReadOnlyList<string> StandardFonts { get; } =
    [
        "Helvetica",
        "Times",
        "Courier",
    ];

    public static IReadOnlyList<string> HorizontalAlignments { get; } =
    [
        "Left",
        "Center",
        "Right",
    ];

    public static IReadOnlyList<string> LineStyles { get; } =
    [
        "Solid",
        "Dashed",
        "Dotted",
    ];

    public static IReadOnlyList<string> Arrowheads { get; } =
    [
        "Open",
        "Filled",
        "Diamond",
    ];

    public static IReadOnlyList<string> FormOverlayModes { get; } =
    [
        "Overlay — click fields on the page",
        "List fields — classic picker",
        "AutoFill from profile",
        "Edit AutoFill profile",
    ];

    public static IReadOnlyList<string> NoteColors { get; } =
    [
        "None",
        "White",
        "Yellow",
        "Light blue",
        "Light green",
    ];

    public static IReadOnlyList<string> CropUnits { get; } =
    [
        "Points (pt)",
        "Inches (in)",
        "Centimeters (cm)",
        "Millimeters (mm)",
    ];

    public static IReadOnlyList<string> PrintScales { get; } =
    [
        "Fit to printable area",
        "Fill page",
        "Actual size",
    ];

    public static IReadOnlyList<string> PagesPerSheet { get; } =
    [
        "1",
        "2",
        "4",
    ];

    public const string NoneChoice = "(none)";
}
