namespace Glyph.Core.Documents;

/// <summary>
/// Temp PNG name for File → New from Clipboard (F01-14).
/// </summary>
public static class ClipboardImageFileName
{
    public static string ForTimestamp(DateTime localNow) =>
        $"Clipboard-{localNow:yyyyMMdd-HHmmss}.png";
}
