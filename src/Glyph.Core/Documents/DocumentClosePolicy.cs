namespace Glyph.Core.Documents;

/// <summary>
/// Close-tab / close-all dirty prompting (F01-22 / F50-03).
/// </summary>
public static class DocumentClosePolicy
{
    public const string UnsavedTitle = "Unsaved changes";
    public const string CloseButton = "Close";
    public const string CancelButton = "Cancel";

    public static bool RequiresDirtyPrompt(bool sessionDirty, bool tabHasUnsavedEdits, bool skipDirtyPrompt) =>
        !skipDirtyPrompt && (sessionDirty || tabHasUnsavedEdits);

    public static string UnsavedClosePrompt(string displayName) =>
        $"“{displayName}” has unsaved changes. Close anyway?";
}
