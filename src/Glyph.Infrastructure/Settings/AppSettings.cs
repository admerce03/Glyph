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

    /// <summary>Default annotation author for sticky notes / text markup (F55-10).</summary>
    public string AnnotationAuthor { get; set; } = string.Empty;

    /// <summary>When true, document toolbars use tighter padding (F02-04 / F54-20).</summary>
    public bool CompactToolbar { get; set; }

    /// <summary>Default highlight preset name (F55-18), e.g. Yellow.</summary>
    public string DefaultHighlightColor { get; set; } = "Yellow";

    /// <summary>Default stroke/ink preset name (F55-18), e.g. Red.</summary>
    public string DefaultStrokeColor { get; set; } = "Red";

    /// <summary>Default sticky-note preset name (F55-18).</summary>
    public string DefaultStickyNoteColor { get; set; } = "Yellow";

    /// <summary>Default stroke width in PDF points (F55-19).</summary>
    public double DefaultStrokeWidthPoints { get; set; } = 2;

    /// <summary>When true, multi-frame images start playing on open (F55-17).</summary>
    public bool AnimationAutoplay { get; set; }

    /// <summary>
    /// When true, Convert/export defaults to stripping EXIF/IPTC/XMP (F55-25).
    /// </summary>
    public bool StripMetadataByDefault { get; set; }

    public bool SidebarVisible { get; set; } = true;
}
