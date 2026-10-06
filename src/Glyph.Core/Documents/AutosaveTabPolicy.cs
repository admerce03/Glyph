namespace Glyph.Core.Documents;

/// <summary>
/// Preferences Auto-save to original (F50-04 / F01-24 / F55-12).
/// When enabled, the recovery timer saves every dirty open tab to its original path
/// (not only the active tab); failures fall back to a recovery snapshot.
/// </summary>
public static class AutosaveTabPolicy
{
    public const bool SavesAllDirtyOpenTabs = true;

    public const bool ActiveTabOnly = false;

    public const string PreferenceKey = "AutoSaveToOriginal";

    public const string Reason =
        "AutoSaveToOriginal persists every dirty PDF/image tab to its original path on the recovery timer; inactive tabs are included.";
}
