namespace Glyph.Core.Documents;

/// <summary>
/// Honest scope of F01-05 / F55-02 session restore (per-window tab path lists).
/// </summary>
public static class SessionRestorePolicy
{
    /// <summary>Preferences can restore a path list into one window as tabs.</summary>
    public const bool RestoresSingleWindowTabList = true;

    /// <summary>
    /// Multi-window layouts are persisted via <c>SessionState.Windows</c> and restored
    /// by reopening each window's path list (bounds/position not restored).
    /// </summary>
    public const bool RestoresMultiWindowLayout = true;

    /// <summary>Window size, position, and monitor placement are not persisted.</summary>
    public const bool RestoresWindowBounds = false;

    public const string ScopeReason =
        "SessionState.Windows stores per-window Paths + ActiveIndex; restore reopens each window's tabs. Legacy flat Paths migrates to one window. Bounds not restored.";
}
