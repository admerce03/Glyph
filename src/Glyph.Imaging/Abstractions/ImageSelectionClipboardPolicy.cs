namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Image selection copy/cut status strings (F28-09/10).
/// </summary>
public static class ImageSelectionClipboardPolicy
{
    public const string NeedSelection = "Make a selection first.";
    public const string EmptyClipboard = "Clipboard is empty — copy or cut a selection first.";
    public const string MenuCopySelection = "Copy selection";
    public const string MenuCopyImage = "Copy image";
    public const string MenuCutSelection = "Cut selection";

    public static string Copied(bool inverted, int width, int height)
    {
        var label = inverted ? "inverted selection" : "selection";
        return $"Copied {label} {width}×{height}.";
    }

    public static string Cut(bool inverted, int width, int height) =>
        inverted
            ? $"Cut inverted selection (kept {width}×{height} hole)."
            : $"Cut selection {width}×{height}.";

    public static string CopyFailed(string message) => "Copy selection failed: " + message;
}
