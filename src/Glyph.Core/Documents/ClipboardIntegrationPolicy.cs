namespace Glyph.Core.Documents;

/// <summary>
/// Clipboard path / image paste entry points (F40-07 / F40-10).
/// </summary>
public static class ClipboardIntegrationPolicy
{
    public const bool PasteImageIntoImageDocument = true;
    public const bool CopyFilePathSupported = true;
    public const bool CopyFileStorageItemSupported = true;

    /// <summary>Ctrl+V with empty selection may open a pasted file path.</summary>
    public const bool PastePathOpensWhenEmpty = true;
}
