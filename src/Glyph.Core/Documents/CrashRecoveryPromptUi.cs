namespace Glyph.Core.Documents;

/// <summary>
/// Crash-recovery reopen prompt (F50-05 / F50-06).
/// </summary>
public static class CrashRecoveryPromptUi
{
    public const string Title = "Recover unsaved work?";
    public const string RecoverButton = "Recover";
    public const string KeepButton = "Keep for later";
    public const string DiscardButton = "Discard";

    public static string Content(string namesSummary) =>
        $"Glyph found crash-recovery copies for: {namesSummary}.";

    public static string NamesSummary(IReadOnlyList<string> displayNames, int previewLimit = 5)
    {
        if (displayNames.Count == 0)
        {
            return string.Empty;
        }

        var shown = displayNames.Take(previewLimit).ToList();
        var text = string.Join(", ", shown);
        if (displayNames.Count > previewLimit)
        {
            text += $" (+{displayNames.Count - previewLimit} more)";
        }

        return text;
    }

    public static string OpenedStatus(int count) =>
        $"Opened {count} recovered document(s).";

    public const string DiscardedStatus = "Discarded crash-recovery copies.";
    public const string KeptStatus = "Crash-recovery copies kept on disk.";
}
