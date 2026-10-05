namespace Glyph.Core.Documents;

/// <summary>
/// Throttles intermediate ScrollViewer render refreshes during fling (F57-08).
/// </summary>
public static class IntermediateScrollThrottle
{
    /// <summary>Minimum milliseconds between intermediate scroll render passes.</summary>
    public const int MinIntervalMs = 72;

    /// <summary>
    /// Returns true when enough time has elapsed since <paramref name="lastTickMs"/>
    /// to schedule another intermediate render. Final (non-intermediate) scrolls
    /// should always render and are not gated here.
    /// </summary>
    public static bool ShouldRender(long nowTickMs, long lastTickMs, int minIntervalMs = MinIntervalMs) =>
        nowTickMs - lastTickMs >= minIntervalMs;
}
