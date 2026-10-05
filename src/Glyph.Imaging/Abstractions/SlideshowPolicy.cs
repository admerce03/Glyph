namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Folder slideshow timing and status (F26-20).
/// </summary>
public static class SlideshowPolicy
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    public const int MinimumImages = 2;

    public const string NeedsMoreImages = "Slideshow needs at least two images in the folder.";
    public const string Started = "Slideshow on — advances every 3s (Esc to stop).";
    public const string Stopped = "Slideshow stopped.";
    public const string StoppedNotEnough = "Slideshow stopped — not enough images.";
    public const string ButtonIdle = "Slideshow";
    public const string ButtonActive = "Stop show";

    public static bool CanStart(int siblingCount) => siblingCount >= MinimumImages;

    public static string ButtonLabel(bool active) => active ? ButtonActive : ButtonIdle;
}
