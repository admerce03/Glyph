namespace Glyph.Core.Documents;

/// <summary>
/// Pure decisions for tearing a tab into a new window (F02-11 / F59-03).
/// </summary>
public static class TabTearOffPolicy
{
    public const string NeedsSaveStatus = "Save the document before moving it to a new window.";
    public const string MovedStatus = "Moved tab to a new window.";
    public const string SaveCancelledStatus = "Save cancelled — tab not moved.";
    public const string RecoveryFailedStatus = "Could not create recovery copy for move.";

    /// <summary>Tear-off requires a path that still exists on disk.</summary>
    public static bool CanTearOff(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    /// <summary>Dirty tabs need an explicit save/recovery choice before tear-off.</summary>
    public static bool RequiresDirtyResolution(bool sessionDirty, bool tabHasUnsavedEdits) =>
        sessionDirty || tabHasUnsavedEdits;

    public static string UnsavedChangesPrompt(string displayName) =>
        $"“{displayName}” has unsaved changes. Save before moving to a new window?";
}
