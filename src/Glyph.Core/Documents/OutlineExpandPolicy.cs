namespace Glyph.Core.Documents;

/// <summary>
/// Default outline TreeView expand/collapse behavior (F05-03).
/// </summary>
public static class OutlineExpandPolicy
{
    /// <summary>New outline nodes start expanded so hierarchy is visible.</summary>
    public const bool DefaultIsExpanded = true;

    public static bool ToggleExpanded(bool currentlyExpanded) => !currentlyExpanded;
}
