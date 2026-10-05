namespace Glyph.Core.Documents;

/// <summary>
/// On-page Find highlight color (F06-09) and clear status (F06-15).
/// </summary>
public static class PdfSearchHighlightStyle
{
    /// <summary>Semi-transparent gold ARGB used for match overlays.</summary>
    public const byte Alpha = 90;
    public const byte Red = 255;
    public const byte Green = 215;
    public const byte Blue = 0;

    public const string ClearedStatus = "Search cleared.";

    public static bool IsGoldHighlight(byte a, byte r, byte g, byte b) =>
        a == Alpha && r == Red && g == Green && b == Blue;
}
