namespace Glyph.Core.Documents;

/// <summary>
/// Honest scope of F01-05 / F55-02 session restore (single-window tab path list).
/// </summary>
public static class SessionRestorePolicy
{
    /// <summary>Preferences can restore a flat path list into one window as tabs.</summary>
    public const bool RestoresSingleWindowTabList = true;

    /// <summary>
    /// Multi-window layouts are not persisted or restored. Each window shares one
    /// <c>JsonSessionStore</c> file (last writer wins).
    /// </summary>
    public const bool RestoresMultiWindowLayout = false;

    public const string ScopeReason =
        "SessionState stores Paths + ActiveIndex only; restore opens tabs in the first window.";
}
