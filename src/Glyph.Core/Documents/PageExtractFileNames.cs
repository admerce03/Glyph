namespace Glyph.Core.Documents;

/// <summary>
/// Temp file names for drag-out page extract (F11-07 deferred StorageItems).
/// </summary>
public static class PageExtractFileNames
{
    public const string ExtractPrefix = "Glyph-pages-";

    public static string TempPdfPath(string tempDirectory, Guid id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tempDirectory);
        return Path.Combine(tempDirectory, ExtractPrefix + id.ToString("N") + ".pdf");
    }
}
