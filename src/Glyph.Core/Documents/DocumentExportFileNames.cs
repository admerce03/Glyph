namespace Glyph.Core.Documents;

/// <summary>
/// File-name helpers for document export paths (F45 page images).
/// </summary>
public static class DocumentExportFileNames
{
    /// <summary>
    /// <c>{baseName}-p{pageNumber1Based}{extension}</c> with a leading dot on the extension when missing.
    /// </summary>
    public static string PageImage(string baseName, int pageNumber1Based, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        if (pageNumber1Based < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber1Based));
        }

        var ext = string.IsNullOrWhiteSpace(extension)
            ? ".png"
            : extension.StartsWith('.') ? extension : "." + extension;
        return $"{baseName}-p{pageNumber1Based}{ext}";
    }
}
