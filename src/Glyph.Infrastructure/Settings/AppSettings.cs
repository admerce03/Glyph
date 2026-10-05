namespace Glyph.Infrastructure.Settings;

public sealed class AppSettings
{
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public int RecentFileCapacity { get; set; } = 20;

    public bool RestorePreviousSession { get; set; }

    /// <summary>When true, dirty documents are written back to their original path on a timer (F50-04).</summary>
    public bool AutoSaveToOriginal { get; set; }

    /// <summary>
    /// Seconds between crash-recovery snapshots. 0 disables periodic recovery writes (F55-21 / F50-02).
    /// </summary>
    public int CrashRecoveryIntervalSeconds { get; set; } = 120;

    public bool SidebarVisible { get; set; } = true;
}
