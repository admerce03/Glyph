namespace Glyph.Core.Documents;

/// <summary>
/// View → Hide/Show Toolbar menu label (F02-05).
/// </summary>
public static class ToolbarVisibilityLabel
{
    public static string For(bool isVisible) => isVisible ? "Hide Toolbar" : "Show Toolbar";
}
