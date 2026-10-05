namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Lists image files in the same folder as a given path for next/previous navigation.
/// </summary>
public static class ImageFolderNavigator
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".bmp",
        ".tif",
        ".tiff",
        ".webp",
        ".heic",
        ".heif",
        ".avif",
        ".ico",
        ".jp2",
        ".j2k",
    };

    public static bool IsImagePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return ImageExtensions.Contains(Path.GetExtension(path));
    }

    /// <summary>
    /// Returns sorted absolute paths of image files in the same directory as
    /// <paramref name="currentPath"/>. Returns empty when the path has no directory.
    /// </summary>
    public static IReadOnlyList<string> ListSiblings(string currentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(currentPath));
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(directory)
            .Where(IsImagePath)
            .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static int IndexOf(IReadOnlyList<string> siblings, string currentPath)
    {
        ArgumentNullException.ThrowIfNull(siblings);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPath);
        var full = Path.GetFullPath(currentPath);
        for (var i = 0; i < siblings.Count; i++)
        {
            if (string.Equals(Path.GetFullPath(siblings[i]), full, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    public static string? Previous(IReadOnlyList<string> siblings, string currentPath)
    {
        var index = IndexOf(siblings, currentPath);
        return index > 0 ? siblings[index - 1] : null;
    }

    public static string? Next(IReadOnlyList<string> siblings, string currentPath)
    {
        var index = IndexOf(siblings, currentPath);
        return index >= 0 && index < siblings.Count - 1 ? siblings[index + 1] : null;
    }
}
