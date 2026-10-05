namespace Glyph.Core.IO;

/// <summary>
/// Path normalization for open/save across local disks, UNC shares, OneDrive, and long paths (F01-10/11).
/// </summary>
public static class PathUtilities
{
    private const int WindowsMaxPath = 260;

    /// <summary>
    /// Trim quotes, resolve to a full path when possible, and apply the Windows long-path prefix
    /// when the resolved path would exceed the legacy MAX_PATH limit.
    /// </summary>
    public static string NormalizeOpenPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var trimmed = path.Trim().Trim('"');
        if (trimmed.Length == 0)
        {
            return path;
        }

        if (trimmed.StartsWith(@"\\?\", StringComparison.Ordinal)
            || trimmed.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        string full;
        try
        {
            full = Path.GetFullPath(trimmed);
        }
        catch (Exception)
        {
            return trimmed;
        }

        if (!OperatingSystem.IsWindows() || full.Length < WindowsMaxPath)
        {
            return full;
        }

        if (full.StartsWith(@"\\", StringComparison.Ordinal))
        {
            // \\server\share\… → \\?\UNC\server\share\…
            return @"\\?\UNC\" + full.AsSpan(2).ToString();
        }

        return @"\\?\" + full;
    }

    public static bool FileExists(string path)
    {
        try
        {
            return File.Exists(NormalizeOpenPath(path));
        }
        catch
        {
            return false;
        }
    }
}
