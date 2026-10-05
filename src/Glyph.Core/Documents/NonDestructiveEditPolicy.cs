namespace Glyph.Core.Documents;

/// <summary>
/// Non-destructive edit posture until Save (F61).
/// </summary>
public static class NonDestructiveEditPolicy
{
    public const bool CropBoxPreservesSourcePixels = true;
    public const bool ImageEditsStayInMemoryUntilSave = true;
    public const bool MarkupOverlayRequiresFlattenBeforeRasterSave = true;

    public static bool ShouldPromptFlatten(bool hasPendingMarkup, bool targetIsRaster) =>
        hasPendingMarkup && targetIsRaster;
}
