namespace Glyph.Core.Documents;

/// <summary>
/// Seed empty note/textbox/form fields from clipboard text (F40-09).
/// </summary>
public static class ClipboardTextSeedPolicy
{
    public static bool ShouldSeed(string? currentText) => string.IsNullOrEmpty(currentText);

    public static string? ApplyClipboardText(string? currentText, string? clipboardText) =>
        ShouldSeed(currentText) && !string.IsNullOrEmpty(clipboardText)
            ? clipboardText
            : currentText;
}
