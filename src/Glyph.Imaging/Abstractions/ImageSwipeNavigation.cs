namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Horizontal swipe → prev/next in folder (F26-21).
/// </summary>
public static class ImageSwipeNavigation
{
    public const double MinimumDeltaPixels = 80;
    public const double HorizontalDominanceFactor = 1.5;

    public enum Direction
    {
        None,
        Next,
        Previous,
    }

    /// <summary>
    /// Swipe left → next; swipe right → previous. Ignores mostly-vertical pans.
    /// </summary>
    public static Direction Resolve(double deltaX, double deltaY = 0) =>
        Math.Abs(deltaX) < MinimumDeltaPixels
        || Math.Abs(deltaX) < Math.Abs(deltaY) * HorizontalDominanceFactor
            ? Direction.None
            : deltaX < 0
                ? Direction.Next
                : Direction.Previous;

    public static int SiblingStep(Direction direction) =>
        direction switch
        {
            Direction.Next => 1,
            Direction.Previous => -1,
            _ => 0,
        };
}
