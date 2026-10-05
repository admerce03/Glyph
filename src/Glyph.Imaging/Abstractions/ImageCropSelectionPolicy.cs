namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Crop-to-selection status strings (F28-13 / F30-01).
/// </summary>
public static class ImageCropSelectionPolicy
{
    public const string CannotCropInverted =
        "Cannot crop an inverted selection — Invert again or Deselect.";
    public const string NeedRegion = "Drag a crop region first.";
    public const string ImageNotReady = "Image not ready to crop.";
    public const string NeedIntegerBox = "Crop needs x,y,w,h integers.";
    public const string InteractiveHint =
        "Drag on the image to select a crop region (aspect from dropdown).";

    public static string CroppedToSelection(int width, int height) =>
        $"Cropped to selection {width}×{height}.";

    public static string CroppedTo(int width, int height) =>
        $"Cropped to {width}×{height}.";

    public static string SelectionPreview(int width, int height) =>
        $"Crop selection → {width}×{height} px";
}
