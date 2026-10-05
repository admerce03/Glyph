namespace Glyph.Core.Documents;

/// <summary>
/// Scan dialog labels and DPI bounds (F42).
/// </summary>
public static class ScanDialogUi
{
    public const string Title = "Scan";
    public const string PrimaryButton = "Scan";
    public const string Cancelled = "Scan cancelled.";
    public const string DuplexLabel = "Duplex (feeder)";
    public const string StraightenLabel = "Straighten (deskew after scan)";

    public static IReadOnlyList<string> SourceLabels { get; } =
    [
        "Flatbed",
        "Feeder (ADF)",
        "Auto",
    ];

    public static IReadOnlyList<string> ColorModeLabels { get; } =
    [
        "Color",
        "Grayscale",
        "Black and white",
    ];

    public static IReadOnlyList<string> DestinationLabels { get; } =
    [
        "Open as images",
        "New PDF",
        "Insert into current PDF",
    ];

    public static IReadOnlyList<string> AutoCropLabels { get; } =
    [
        "Off",
        "Single region",
        "Multiple photos (flatbed)",
    ];

    public static IReadOnlyList<string> PaperSizeLabels { get; } =
    [
        "Device default",
        "Letter (8.5×11)",
        "Legal (8.5×14)",
        "A4",
        "A5",
        "A3",
        "Tabloid (11×17)",
        "Statement (5.5×8.5)",
        "Auto-detect (feeder)",
    ];

    public const int MinDpi = 150;
    public const int MaxDpi = 600;

    public static int ClampDpi(int dpi) => Math.Clamp(dpi, MinDpi, MaxDpi);
}
