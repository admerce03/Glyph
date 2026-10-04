using Glyph.Core.Documents;

namespace Glyph.Core.IO;

/// <summary>
/// Lightweight extension-based format detection used before engine-specific probing.
/// </summary>
public static class FileFormatDetector
{
    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff",
        ".webp", ".heic", ".heif", ".avif", ".ico", ".j2k", ".jp2",
    };

    public static DocumentKind DetectKind(string pathOrFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathOrFileName);

        var extension = System.IO.Path.GetExtension(pathOrFileName);
        if (PdfExtensions.Contains(extension))
        {
            return DocumentKind.Pdf;
        }

        if (ImageExtensions.Contains(extension))
        {
            return DocumentKind.Image;
        }

        return DocumentKind.Unknown;
    }

    public static bool IsSupported(string pathOrFileName) =>
        DetectKind(pathOrFileName) != DocumentKind.Unknown;
}
