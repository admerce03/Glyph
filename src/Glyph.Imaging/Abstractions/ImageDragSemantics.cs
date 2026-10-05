namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Pure helpers for image surface drag-out (F59-05).
/// </summary>
public static class ImageDragSemantics
{
    public static bool CanDragFile(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    public static string DraggingStatus => "Dragging image…";
}
