using Glyph.Core.Documents;

namespace Glyph.Core.IO;

/// <summary>
/// Lightweight extension-based format detection used before engine-specific probing.
/// Also owns the shell file-picker filter list so Open / Open Multiple stay in sync (F01-01/02).
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

    /// <summary>
    /// Extensions accepted by File → Open / Open Multiple pickers (stable, sorted).
    /// </summary>
    public static IReadOnlyList<string> SupportedExtensions { get; } = BuildSupportedExtensions();

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

    /// <summary>
    /// Filters a multi-select / drop path list to formats Glyph can open (F01-02).
    /// </summary>
    public static IReadOnlyList<string> FilterSupportedPaths(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return paths.Where(static p => !string.IsNullOrWhiteSpace(p) && IsSupported(p)).ToArray();
    }

    private static string[] BuildSupportedExtensions()
    {
        var list = new List<string>(PdfExtensions.Count + ImageExtensions.Count);
        list.AddRange(PdfExtensions);
        list.AddRange(ImageExtensions);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list.ToArray();
    }
}
