namespace Glyph.Core.Ocr;

/// <summary>
/// Image Live Text overlay Find / entity feedback (F08-02 / F08-05).
/// </summary>
public static class ImageOcrOverlayStatus
{
    public const string RunOcrBeforeSearch = "Run OCR before searching.";
    public const string RunOcrFirst = "Run OCR first.";
    public const string NoMatches = "No OCR matches.";
    public const string NoEntitiesDetected =
        "No URLs, emails, phones, addresses, dates, or times detected.";
    public const string OpenedCalendarInvite = "Opened calendar invite.";

    public static string FormatMatches(int count) => $"OCR find: {count} match(es).";

    public static string FormatMatch(int index1Based, int total, string wordText) =>
        $"OCR match {index1Based}/{total}: {wordText}";

    public static string FormatReady(int wordCount) =>
        wordCount == 0
            ? "OCR finished — no text."
            : $"OCR ready — click words to select ({wordCount} word(s)).";

    public static string FormatEntitiesHeader(int count) =>
        $"{count} entity(ies) in OCR text";

    public static string FormatCopiedEntityUnparsed(string kind) =>
        $"Copied {kind} (could not parse for calendar).";
}
