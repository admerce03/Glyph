namespace Glyph.Core.Documents;

/// <summary>
/// Precision touchpad gesture mapping (F53).
/// </summary>
public static class TouchpadGesturePolicy
{
    /// <summary>Two-finger scroll maps to native ScrollViewer pan.</summary>
    public const bool TwoFingerScrollUsesScrollViewer = true;

    /// <summary>Pinch / Ctrl+wheel maps to zoom (same clamp path as mouse).</summary>
    public static bool PreferPinchZoom(bool controlModifierDown, bool manipulationScale) =>
        controlModifierDown || manipulationScale;

    /// <summary>Glyph does not implement custom touchscreen or pen gesture stacks.</summary>
    public const bool CustomTouchscreenGestures = false;

    public static IReadOnlyList<string> SupportedMappings { get; } =
    [
        "Two-finger scroll → pan",
        "Pinch / Ctrl+wheel → zoom",
    ];
}
