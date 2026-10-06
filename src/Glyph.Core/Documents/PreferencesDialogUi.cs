namespace Glyph.Core.Documents;

/// <summary>
/// Preferences dialog labels (F55).
/// </summary>
public static class PreferencesDialogUi
{
    public const string DialogTitle = "Preferences";
    public const string SaveButton = "Save";
    public const string ThemeHeader = "Theme";
    public const string RestoreTabs =
        "Restore previously open tabs on startup";
    public const string AutoSaveToOriginal =
        "Automatically save changes to the original file";
    public const string CrashRecoveryIntervalHeader =
        "Crash recovery interval (seconds, 0 = off)";
    public const string RecentFileCapacityHeader = "Recent file list capacity";
    public const string VersionSnapshots =
        "Keep local version snapshots on Save";
    public const string SnapshotCapacityHeader = "Snapshots kept per file";
    public const string SeparateWindows =
        "Open each file in a separate window";
    public const string AnnotationAuthorHeader = "Default annotation author";
    public const string CompactToolbar =
        "Compact document toolbars (tighter padding)";
    public const string ToolbarCommandsHeader =
        "Toolbar commands (unchecked = hidden; ↑↓ reorder; applies to open documents)";
    public const string ResetToolbar = "Reset toolbar to default";
    public const string MoveToolbarCommandUp = "↑";
    public const string MoveToolbarCommandDown = "↓";
    public const string ShortcutsHeader =
        "Keyboard shortcuts (blank = default; reopen menus apply immediately)";
    public const string ResetShortcuts = "Reset shortcuts to default";
    public const string DefaultHighlightColorHeader = "Default highlight color";
    public const string DefaultStrokeColorHeader = "Default stroke color";
    public const string DefaultStickyColorHeader = "Default sticky-note color";
    public const string DefaultStrokeWidthHeader = "Default stroke width (pt)";
    public const string AnimationAutoplay =
        "Autoplay animated images on open";
    public const string StripMetadataByDefault =
        "Strip metadata by default when converting images";
    public const string DefaultPdfLayoutHeader = "Default PDF page layout";
    public const string DefaultPdfZoomHeader =
        "Default PDF zoom (scale, e.g. 1.25 = 125%)";
    public const string RememberLastPage =
        "Remember last page when reopening PDFs";
    public const string RememberZoom =
        "Remember zoom when reopening documents";
    public const string FindCaseSensitive =
        "Find: Match case (default)";
    public const string FindAnyWord =
        "Find: Any word (default; off = exact phrase)";
    public const string FindSortByRelevance =
        "Find: Sort by relevance (default; off = page order)";
    public const string ImageZoom100Header = "Image 100% zoom means";
    public const string DefaultInterpolationHeader = "Default resize interpolation";
    public const string ColorManagedDisplay =
        "Color-managed image display by default";
    public const string LocalOcrNote =
        "OCR runs locally via Windows OCR (never uploaded).";
    public const string OcrLanguageHeader =
        "OCR language tag (empty = Windows profile languages)";
    public const string OcrLanguagePlaceholder = "e.g. en-US, de-DE, ja";
    public const string ClearRecentFiles = "Clear recent files";
    public const string ClearSavedSignatures = "Clear saved signatures";
    public const string ClearSignaturesTitle = "Clear saved signatures?";
    public const string ClearSignaturesBody =
        "This permanently deletes all signatures in the local library.";
    public const string ClearButton = "Clear";
    public const string PrivacyHeader = "Privacy";
    public const string CheckForUpdates = "Check for updates…";
    public const string CancelButton = DialogButtons.Cancel;

    public static IReadOnlyList<string> PdfLayoutLabels { get; } =
    [
        "Continuous", "Single page", "Two-up", "Two-up with cover",
    ];

    public static IReadOnlyList<string> ThemeLabels { get; } =
    [
        "System", "Light", "Dark",
    ];

    public static IReadOnlyList<string> ThemeNames { get; } =
    [
        "System", "Light", "Dark",
    ];

    public static IReadOnlyList<string> PdfLayoutNames { get; } =
    [
        "Continuous", "Single", "TwoPage", "TwoPageWithCover",
    ];

    public static IReadOnlyList<string> ImageZoom100Labels { get; } =
    [
        "1:1 pixels", "Print size (use image DPI)",
    ];

    public static IReadOnlyList<string> InterpolationLabels { get; } =
    [
        "Auto", "Nearest-neighbor", "Bilinear", "Bicubic",
    ];

    public static string InterpolationSetting(int selectedIndex) =>
        selectedIndex switch
        {
            1 => "NearestNeighbor",
            2 => "Bilinear",
            3 => "Bicubic",
            _ => "Auto",
        };

    public static int InterpolationIndex(string? setting) =>
        setting switch
        {
            "NearestNeighbor" => 1,
            "Bilinear" => 2,
            "Bicubic" => 3,
            _ => 0,
        };

    public static string Zoom100Setting(int selectedIndex) =>
        selectedIndex == 1 ? "Print" : "Pixels";

    public static int Zoom100Index(string? setting) =>
        string.Equals(setting, "Print", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

    public static int ThemeIndex(string? setting) =>
        setting?.Trim() switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0,
        };

    public static string ThemeSetting(int selectedIndex) =>
        selectedIndex switch
        {
            1 => "Light",
            2 => "Dark",
            _ => "System",
        };
}
