namespace Glyph.Core.Documents;

/// <summary>
/// Scan dialog labels and DPI / page / tone bounds (F42).
/// </summary>
public static class ScanDialogUi
{
    public const string Title = "Scan";
    public const string PrimaryButton = "Scan";
    public const string Cancelled = "Scan cancelled.";
    public const string LookingForScanners = "Looking for scanners…";
    public const string NoScannersFound = "No scanners found.";
    public const string NoScannersDialogTitle = "No scanners";
    public const string NoScannersDialogBody =
        "Windows did not report any image scanners. Connect a scanner and try again.";
    public const string OkButton = "OK";
    public const string ProducedNoFiles = "Scan produced no files.";
    public const string FailedPrefix = "Scan failed: ";
    public const string OpenPdfFirstToInsert = "Open a PDF first to insert scanned pages.";
    public const string PdfEngineUnavailable = "PDF engine unavailable for insert.";
    public const string DuplexLabel = "Duplex (feeder)";
    public const string StraightenLabel = "Straighten (deskew after scan)";
    public const string MaxPagesHeader = "Max pages (feeder)";
    public const string BrightnessHeader = "Brightness (device, if supported)";
    public const string ContrastHeader = "Contrast (device, if supported)";

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
    public const int MinMaxPages = 1;
    public const int MaxMaxPages = 50;
    public const int ToneMin = -1000;
    public const int ToneMax = 1000;
    public const int ToneNeutral = 0;

    public static int ClampDpi(int dpi) => Math.Clamp(dpi, MinDpi, MaxDpi);

    public static int ClampMaxPages(double value) =>
        (int)Math.Clamp(value, MinMaxPages, MaxMaxPages);

    public static int ClampTone(int value) => Math.Clamp(value, ToneMin, ToneMax);

    /// <summary>Null when slider is at neutral (device default).</summary>
    public static int? ToneOrNull(int value)
    {
        var clamped = ClampTone(value);
        return clamped == ToneNeutral ? null : clamped;
    }

    public static string FormatFailed(string message) => FailedPrefix + message;

    public static string FormatScanningWith(string deviceName) =>
        $"Scanning with {deviceName}…";

    public static string FormatStraightening(int count) =>
        $"Straightening {count} scan(s)…";

    public static string FormatOpenedImages(int count) =>
        $"Opened {count} scanned image(s).";

    public static string FormatCreatedPdf(int count) =>
        $"Created PDF from {count} scan(s).";

    public static string FormatInsertedPages(int count) =>
        $"Inserted {count} scanned page(s) into PDF.";
}
