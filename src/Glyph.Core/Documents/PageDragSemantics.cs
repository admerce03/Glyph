namespace Glyph.Core.Documents;

/// <summary>
/// Same-document vs cross-document page-drag decisions (F11-08/09).
/// </summary>
public static class PageDragSemantics
{
    /// <summary>
    /// True when the payload targets the drop document: matching key, or legacy
    /// empty key (same-document reorder payloads).
    /// </summary>
    public static bool IsSameDocument(string? payloadDocumentKey, string dropDocumentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dropDocumentKey);
        if (string.IsNullOrEmpty(payloadDocumentKey))
        {
            return true;
        }

        return string.Equals(payloadDocumentKey, dropDocumentKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// Stable document key for drag registry / payloads (path when available).
    /// </summary>
    public static string DocumentKey(string? path, int identityHash) =>
        string.IsNullOrWhiteSpace(path) ? identityHash.ToString("X") : path;
}
