namespace Glyph.Core.Documents;

/// <summary>
/// Cycle window placement across monitors (F02-19).
/// </summary>
public static class MonitorCyclePolicy
{
    /// <summary>
    /// Next monitor index after <paramref name="currentIndex"/> in a ring of <paramref name="count"/>.
    /// Returns -1 when cycling is not possible.
    /// </summary>
    public static int NextIndex(int currentIndex, int count)
    {
        if (count <= 1)
        {
            return -1;
        }

        if (currentIndex < 0 || currentIndex >= count)
        {
            return 0;
        }

        return (currentIndex + 1) % count;
    }

    public static string SingleMonitorStatus => "Only one monitor detected.";

    public static string MovedStatus(int nextIndex, int count) =>
        $"Moved window to monitor {nextIndex + 1} of {count}.";

    /// <summary>
    /// Center a window size inside a work-area rectangle, clamping to fit.
    /// </summary>
    public static (int X, int Y, int Width, int Height) FitInWorkArea(
        int workX,
        int workY,
        int workWidth,
        int workHeight,
        int windowWidth,
        int windowHeight)
    {
        var width = Math.Min(windowWidth, workWidth);
        var height = Math.Min(windowHeight, workHeight);
        return (
            workX + Math.Max(0, (workWidth - width) / 2),
            workY + Math.Max(0, (workHeight - height) / 2),
            width,
            height);
    }
}
