namespace Glyph.Core.Documents;

/// <summary>
/// Persist/restore of per-window screen bounds for F01-05 session restore.
/// </summary>
public static class SessionWindowBoundsPolicy
{
    public const int MinWidth = 480;
    public const int MinHeight = 320;

    public static bool HasUsableBounds(int width, int height) =>
        width >= MinWidth && height >= MinHeight;

    /// <summary>
    /// Keep the window on a known work area. If the saved rect is completely off-screen,
    /// center it on the first work area (clamped to fit).
    /// </summary>
    public static (int X, int Y, int Width, int Height) ClampToWorkAreas(
        int x,
        int y,
        int width,
        int height,
        IReadOnlyList<(int X, int Y, int Width, int Height)> workAreas)
    {
        ArgumentNullException.ThrowIfNull(workAreas);
        var w = Math.Max(MinWidth, width);
        var h = Math.Max(MinHeight, height);
        if (workAreas.Count == 0)
        {
            return (x, y, w, h);
        }

        var centerX = x + w / 2;
        var centerY = y + h / 2;
        foreach (var area in workAreas)
        {
            if (centerX >= area.X
                && centerX < area.X + area.Width
                && centerY >= area.Y
                && centerY < area.Y + area.Height)
            {
                var clampedW = Math.Min(w, area.Width);
                var clampedH = Math.Min(h, area.Height);
                var clampedX = Math.Clamp(x, area.X, area.X + Math.Max(0, area.Width - clampedW));
                var clampedY = Math.Clamp(y, area.Y, area.Y + Math.Max(0, area.Height - clampedH));
                return (clampedX, clampedY, clampedW, clampedH);
            }
        }

        var primary = workAreas[0];
        return MonitorCyclePolicy.FitInWorkArea(
            primary.X, primary.Y, primary.Width, primary.Height, w, h);
    }
}
