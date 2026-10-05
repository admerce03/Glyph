namespace Glyph.Core.Documents;

/// <summary>
/// Explicit non-goals from FEATURES.md §63 (F63).
/// </summary>
public static class ExplicitExclusionPolicy
{
    public static IReadOnlyList<string> ExcludedCapabilities { get; } =
    [
        "Touchscreen-specific interaction",
        "Stylus / pen / Windows Ink",
        "Pressure-sensitive pen input",
        "Touch-display pinch/pan gestures",
        "Force Touch drawing",
        "Vision Pro / Spatial Preview",
        "macOS Continuity Camera",
        "AirDrop / FaceTime",
        "Apple Maps / Mail / iCloud signatures",
        "macOS document versioning / file locking",
        "Quartz Filters / ColorSync-specific UI",
    ];

    public const bool TouchscreenGesturesSupported = false;
    public const bool StylusInputSupported = false;
    public const bool WindowsInkSupported = false;
}
