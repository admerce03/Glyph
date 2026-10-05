namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Image pixel selection mode status strings (F28-05/07).
/// </summary>
public static class ImagePixelSelectionPolicy
{
    public const string ModeOn =
        "Selection mode — drag a shape; drag inside to move (arrow keys nudge; Esc exits).";
    public const string ModeOff = "Selection mode off.";
    public const string Cleared = "Selection cleared.";
    public const string MenuSelectAll = "Select all";
    public const string MenuDeselect = "Deselect";

    public static string SelectedAll(int width, int height) =>
        $"Selected all {width}×{height}.";

    public static ImageRect FullImageRect(int pixelWidth, int pixelHeight) =>
        new(0, 0, Math.Max(0, pixelWidth), Math.Max(0, pixelHeight));
}
