namespace Glyph.Core.Documents;

/// <summary>
/// Accessibility posture notes covered by WinUI chrome (F56-02…11).
/// </summary>
public static class AccessibilityPolicy
{
    public const bool KeyboardAccessibleChrome = true;
    public const bool UsesWinUiFocusVisuals = true;
    public const bool HighContrastViaThemeResources = true;
    public const bool TextScalingViaXamlRoot = true;
    public const bool DocumentViewsAreTabStops = true;
    public const bool ZoomScalesPageBitmapsOnly = true;
    public const bool ImageDescriptionMapsToAutomationName = true;
    public const bool SignatureLibrarySupportsDescriptions = true;

    public static IReadOnlyList<string> ScreenReaderLabeledSurfaces { get; } =
    [
        "Toolbar",
        "Search",
        "Annotations",
        "Signatures",
    ];
}
