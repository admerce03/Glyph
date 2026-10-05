namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Markup flatten prompt / status (F34-10).
/// </summary>
public static class ImageMarkupFlattenPolicy
{
    public const string DialogTitle = "Flatten markup?";
    public const string PrimaryButton = "Flatten & continue";
    public const string NothingToFlatten = "No markup to flatten.";

    public static string Flattened(int itemCount) =>
        $"Flattened {itemCount} markup item(s).";

    public static bool HasPendingMarkup(int strokeCount, int shapeCount) =>
        strokeCount > 0 || shapeCount > 0;
}
