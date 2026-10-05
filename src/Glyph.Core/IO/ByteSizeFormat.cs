namespace Glyph.Core.IO;

/// <summary>
/// Human-readable byte sizes for status/info UI (files, optimize estimates).
/// </summary>
public static class ByteSizeFormat
{
    public static string Format(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes + " B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.#} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):0.##} MB";
    }

    public static string FormatOptional(long? bytes, string nullLabel = "—") =>
        bytes is { } size ? Format(size) : nullLabel;
}
