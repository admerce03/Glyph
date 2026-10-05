namespace Glyph.Core.Documents;

/// <summary>
/// PDF page Export toolbar format names / extensions (F01-18).
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
}
