namespace Glyph.Core.Documents;

/// <summary>
/// Crash-recovery autosave interval prefs (F55-21).
/// 0 disables the timer; active intervals clamp to a practical range.
/// </summary>
public static class CrashRecoveryIntervalPolicy
{
    public const int DisabledSeconds = 0;
    public const int MinActiveSeconds = 15;
    public const int MaxActiveSeconds = 3600;

    /// <summary>True when Preferences interval should run a DispatcherTimer.</summary>
    public static bool IsEnabled(int seconds) => seconds > DisabledSeconds;

    /// <summary>Clamp a positive interval for timer use (caller must skip when disabled).</summary>
    public static int ClampActiveSeconds(int seconds) =>
        Math.Clamp(seconds, MinActiveSeconds, MaxActiveSeconds);
}
