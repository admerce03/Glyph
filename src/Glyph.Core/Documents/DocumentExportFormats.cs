namespace Glyph.Core.Documents;

/// <summary>
/// PDF page Export toolbar format names / extensions / DPI·quality (F01-18 / F45).
/// </summary>
public static class DocumentExportFormats
{
    public static IReadOnlyList<string> PageImageFormatNames { get; } =
    [
        "PNG", "JPEG", "WebP", "TIFF", "BMP", "GIF", "AVIF", "JPEG 2000",
    ];

    public static string ExtensionForDisplayName(string? formatName) =>
        (formatName ?? "PNG") switch
        {
            "JPEG" => ".jpg",
            "WebP" => ".webp",
            "TIFF" => ".tif",
            "BMP" => ".bmp",
            "GIF" => ".gif",
            "AVIF" => ".avif",
            "JPEG 2000" => ".jp2",
            _ => ".png",
        };

    public const string CancelledStatus = "Export cancelled.";
    public const string DialogTitle = "Export pages";
    public const string PrimaryButton = "Export…";
    public const string QualityHeader = "JPEG/WebP/AVIF quality";
    public const string DpiHeader = "Render DPI";
    public const string WebpLosslessLabel = "Lossless WebP";
    public const string ExportingPage = "Exporting page…";
    public const string ExportingCroppedPdf = "Exporting permanently cropped PDF…";
    public const double DefaultDpi = 144;
    public const double MinDpi = 36;
    public const double MaxDpi = 600;
    public const int MinQuality = 1;
    public const int MaxQuality = 100;
    public const int DefaultQuality = 85;

    public static bool SupportsLosslessCompression(string? formatName) =>
        formatName is "WebP" or "PNG" or "TIFF" or "AVIF";

    public static bool AnnotationsFlattenedInRasterExport => true;

    public static double ParseDpi(string? text, double fallback = DefaultDpi)
    {
        if (!double.TryParse(text, out var dpi) || dpi < MinDpi || dpi > MaxDpi)
        {
            return fallback;
        }

        return dpi;
    }

    public static int ClampQuality(double value) =>
        (int)Math.Clamp(value, MinQuality, MaxQuality);

    /// <summary>JPEG / JPEG 2000 / BMP / GIF flatten alpha; PNG / WebP / TIFF / AVIF keep it.</summary>
    public static bool FlattensTransparency(string? formatName) =>
        formatName is "JPEG" or "JPEG 2000" or "BMP" or "GIF";

    public static string ExportSummary(int pageCount) =>
        $"Export {pageCount} page(s) as image(s)";

    public static string FormatExportingPages(int pageCount) =>
        $"Exporting {pageCount} pages…";

    public static string FormatExportProgress(int written, int total) =>
        $"Exporting {written}/{total}…";

    public static string FormatExportedPage(int pageNumber1Based, string fileName) =>
        $"Exported page {pageNumber1Based} to {fileName}.";

    public static string FormatExportedPages(int written, string folderName) =>
        $"Exported {written} page image(s) to {folderName}.";

    public static string FormatExportedCroppedPdf(string fileName) =>
        $"Exported cropped PDF to {fileName}.";

    public static string FormatExportedBookmarks(int bookmarkCount) =>
        $"Exported {bookmarkCount} bookmark(s) into the PDF outline.";

    public const string SuggestedOcrSearchable = "OCR searchable";
    public const string SuggestedExtractedPages = "Extracted pages";
    public const string SuggestedCroppedPages = "Cropped pages";
}
