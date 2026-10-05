namespace Glyph.Core.Documents;

/// <summary>
/// Thumbnail sidebar context-menu labels (F02-14 / F03-17).
/// </summary>
public static class ThumbnailContextMenu
{
    public static string RotateLeft(int count) =>
        count == 1 ? "Rotate left" : $"Rotate left ({count})";

    public static string RotateRight(int count) =>
        count == 1 ? "Rotate right" : $"Rotate right ({count})";

    public static string Duplicate(int count) =>
        count == 1 ? "Duplicate" : $"Duplicate ({count})";

    public static string Extract(int count) =>
        count == 1 ? "Extract…" : $"Extract ({count})…";

    public static string CopyPages(int count) =>
        count == 1 ? "Copy page" : $"Copy pages ({count})";

    public const string InsertBlankAfter = "Insert blank after";

    public static string Delete(int count) =>
        count == 1 ? "Delete" : $"Delete ({count})";
}
