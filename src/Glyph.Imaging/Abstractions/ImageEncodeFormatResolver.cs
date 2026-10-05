namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Maps Magick format names and file extensions to <see cref="ImageEncodeFormat"/>
/// for Save As, crash recovery, and batch re-encode (F01-17 / F62).
/// </summary>
public static class ImageEncodeFormatResolver
{
    /// <summary>
    /// Resolves Magick <c>FormatName</c> (or similar) to encode format + preferred extension.
    /// Unknown names fall back to PNG.
    /// </summary>
    public static (ImageEncodeFormat Format, string Extension) FromFormatName(string? formatName)
    {
        var name = formatName ?? string.Empty;

        // Order matters: Jpeg2000 contains "Jpeg"; check JP2/JPEG 2000 first.
        if (ContainsAny(name, "Jpeg2000", "JPEG 2000", "Jp2", "J2k", "J2K"))
        {
            return (ImageEncodeFormat.Jpeg2000, ".jp2");
        }

        if (ContainsAny(name, "Jpeg", "Jpg"))
        {
            return (ImageEncodeFormat.Jpeg, ".jpg");
        }

        if (ContainsAny(name, "WebP", "Webp"))
        {
            return (ImageEncodeFormat.Webp, ".webp");
        }

        if (ContainsAny(name, "Tif"))
        {
            return (ImageEncodeFormat.Tiff, ".tif");
        }

        if (ContainsAny(name, "Bmp"))
        {
            return (ImageEncodeFormat.Bmp, ".bmp");
        }

        if (ContainsAny(name, "Gif"))
        {
            return (ImageEncodeFormat.Gif, ".gif");
        }

        if (ContainsAny(name, "Avif"))
        {
            return (ImageEncodeFormat.Avif, ".avif");
        }

        if (ContainsAny(name, "Heic", "Heif"))
        {
            return (ImageEncodeFormat.Heic, ".heic");
        }

        if (ContainsAny(name, "Png"))
        {
            return (ImageEncodeFormat.Png, ".png");
        }

        if (ContainsAny(name, "Pdf"))
        {
            return (ImageEncodeFormat.Pdf, ".pdf");
        }

        return (ImageEncodeFormat.Png, ".png");
    }

    /// <summary>
    /// Resolves a file extension (with or without leading dot) to an encode format.
    /// </summary>
    public static ImageEncodeFormat FromExtension(string? extension)
    {
        var ext = (extension ?? string.Empty).Trim();
        if (!ext.StartsWith('.'))
        {
            ext = "." + ext;
        }

        return ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImageEncodeFormat.Jpeg,
            ".webp" => ImageEncodeFormat.Webp,
            ".tif" or ".tiff" => ImageEncodeFormat.Tiff,
            ".bmp" => ImageEncodeFormat.Bmp,
            ".gif" => ImageEncodeFormat.Gif,
            ".avif" => ImageEncodeFormat.Avif,
            ".jp2" or ".j2k" => ImageEncodeFormat.Jpeg2000,
            ".heic" or ".heif" => ImageEncodeFormat.Heic,
            ".pdf" => ImageEncodeFormat.Pdf,
            _ => ImageEncodeFormat.Png,
        };
    }

    /// <summary>
    /// Preferred extension for a known encode format.
    /// </summary>
    public static string ExtensionFor(ImageEncodeFormat format) =>
        format switch
        {
            ImageEncodeFormat.Jpeg => ".jpg",
            ImageEncodeFormat.Webp => ".webp",
            ImageEncodeFormat.Tiff => ".tif",
            ImageEncodeFormat.Bmp => ".bmp",
            ImageEncodeFormat.Gif => ".gif",
            ImageEncodeFormat.Avif => ".avif",
            ImageEncodeFormat.Jpeg2000 => ".jp2",
            ImageEncodeFormat.Heic => ".heic",
            ImageEncodeFormat.Pdf => ".pdf",
            _ => ".png",
        };

    private static bool ContainsAny(string name, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (name.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
