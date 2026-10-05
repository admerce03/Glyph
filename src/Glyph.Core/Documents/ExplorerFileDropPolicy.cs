namespace Glyph.Core.Documents;

/// <summary>
/// Explorer → Glyph window file-drop acceptance (F01-08).
/// Page-drag text payloads stay with thumbnail targets instead of opening as files.
/// </summary>
public static class ExplorerFileDropPolicy
{
    public static bool ShouldOpenDroppedFiles(bool hasStorageItems, bool textLooksLikePageDrag) =>
        hasStorageItems && !textLooksLikePageDrag;

    public static string DragCaption => "Open in Glyph";
}
