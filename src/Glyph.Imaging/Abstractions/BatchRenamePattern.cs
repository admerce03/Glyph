namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Batch rename pattern expansion (F36-08).
/// </summary>
public static class BatchRenamePattern
{
    public const string DefaultPattern = "{name}-{n:000}";

    public static string Expand(string pattern, string baseName, int oneBasedIndex)
    {
        var stem = (pattern ?? DefaultPattern)
            .Replace("{name}", baseName, StringComparison.OrdinalIgnoreCase)
            .Replace("{n:000}", oneBasedIndex.ToString("000"), StringComparison.OrdinalIgnoreCase)
            .Replace("{n:00}", oneBasedIndex.ToString("00"), StringComparison.OrdinalIgnoreCase)
            .Replace("{n}", oneBasedIndex.ToString(), StringComparison.OrdinalIgnoreCase);

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            stem = stem.Replace(c, '_');
        }

        return stem.Trim();
    }
}
