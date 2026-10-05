namespace Glyph.Core.Documents;

/// <summary>
/// Distinguishes plain mouse-wheel scroll from Ctrl+wheel zoom (F04-20 / F02-22).
/// </summary>
public static class WheelInputPolicy
{
    /// <summary>
    /// True when the wheel event should adjust zoom instead of scrolling.
    /// </summary>
    public static bool PreferZoomOverScroll(bool controlModifierDown) => controlModifierDown;
}
