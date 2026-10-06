namespace Glyph.Core.Documents;

/// <summary>
/// Honest scope of F01-05 / F55-02 session restore (per-window tab path lists + bounds).
/// </summary>
public static class SessionRestorePolicy
{
    /// <summary>Preferences can restore a path list into one window as tabs.</summary>
    public const bool RestoresSingleWindowTabList = true;

    /// <summary>
    /// Multi-window layouts are persisted via <c>SessionState.Windows</c> and restored
    /// by reopening each window's path list.
    /// </summary>
    public const bool RestoresMultiWindowLayout = true;

    /// <summary>
    /// Window size/position (and maximized) are persisted per window and clamped to
    /// available work areas on restore via <see cref="SessionWindowBoundsPolicy"/>.
    /// </summary>
    public const bool RestoresWindowBounds = true;

    public const string ScopeReason =
        "SessionState.Windows stores per-window Paths + ActiveIndex + bounds; restore reopens each window's tabs and applies clamped bounds. Legacy flat Paths migrates to one window.";
}
