namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Image viewer pan contract (F26-13): ScrollViewer hosts the bitmap surface.
/// </summary>
public static class ImagePanPolicy
{
    public const bool UsesScrollViewer = true;
    public const bool WheelZoomsIndependently = true;

    public static IReadOnlyList<string> DeclaredBehaviors { get; } =
    [
        "ScrollViewer pans when content exceeds viewport",
        "Pointer wheel adjusts zoom (ZoomMode disabled on ScrollViewer)",
        "Folder swipe navigation does not fight ScrollViewer pan until release",
    ];
}
