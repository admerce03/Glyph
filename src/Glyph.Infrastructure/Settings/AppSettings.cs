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

    /// <summary>Sidebar width in DIPs when visible (F02-07).</summary>
    public double SidebarWidth { get; set; } = 220;

    /// <summary>When true, each File → Open path opens in a new window (F01-03).</summary>
    public bool OpenFilesInSeparateWindows { get; set; }

    /// <summary>When true, each successful Save keeps a local version snapshot (F51).</summary>
    public bool VersionSnapshotsEnabled { get; set; }

    /// <summary>Max snapshots retained per document path.</summary>
    public int VersionSnapshotCapacity { get; set; } = 5;

    public bool SidebarVisible { get; set; } = true;
}
