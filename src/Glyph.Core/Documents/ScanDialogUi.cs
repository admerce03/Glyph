namespace Glyph.Core.Documents;

/// <summary>
/// Scan dialog labels and DPI bounds (F42).
/// </summary>
public static class ScanDialogUi
{
    public const string Title = "Scan";
    public const string PrimaryButton = "Scan";
    public const string Cancelled = "Scan cancelled.";

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

    public const int MinDpi = 150;
    public const int MaxDpi = 600;

    public static int ClampDpi(int dpi) => Math.Clamp(dpi, MinDpi, MaxDpi);
}
