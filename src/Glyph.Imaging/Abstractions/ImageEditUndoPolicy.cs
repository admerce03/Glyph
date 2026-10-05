namespace Glyph.Imaging.Abstractions;

/// <summary>
/// In-memory edit undo stack policy (F29-09 / F30-08).
/// </summary>
public static class ImageEditUndoPolicy
{
    public const int MaxDepth = 12;
    public const string NothingToUndo = "Nothing to undo.";
    public const string Undone = "Edit undone.";

    public static string Failed(string message) => "Undo failed: " + message;

    public static int TrimCount(int currentCount, int maxDepth = MaxDepth) =>
        Math.Max(0, currentCount - maxDepth);
}
