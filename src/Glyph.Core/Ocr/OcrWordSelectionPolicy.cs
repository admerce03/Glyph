namespace Glyph.Core.Ocr;

/// <summary>
/// OCR word-box overlay selection status (F08-02).
/// </summary>
public static class OcrWordSelectionPolicy
{
    public const string Cleared = "OCR selection cleared.";
    public const string NothingToCopy = "No OCR text to copy.";

    public static string SelectedWord(string wordText) => $"Selected OCR: {wordText}";

    public static string CopiedWords(int selectedCount) =>
        selectedCount == 0
            ? "All OCR text copied."
            : $"Copied {selectedCount} OCR word(s).";

    /// <summary>
    /// Without Ctrl, replace the selection; with Ctrl, toggle membership.
    /// </summary>
    public static bool ShouldClearBeforeToggle(bool ctrlHeld) => !ctrlHeld;
}
