namespace Glyph.Core.Documents;

/// <summary>
/// Status / mailto copy for Windows Share integration (F46 / F47).
/// </summary>
public static class DocumentShareStatus
{
    public const string NothingToShare = "Nothing to share — open a saved document.";
    public const string ShareOpened = "Share UI opened.";
    public const string ShareUnavailable = "Share unavailable.";
    public const string PathCopied = "Copied path.";
    public const string NoPath = "No file path to copy.";
    public const string FileCopied = "Copied file to clipboard.";
    public const string NoFile = "No file to copy.";
    public const string NoFileToOpen = "No file to open.";
    public const string MailOpened = "Mail client opened.";
    public const string MailtoBody = "Shared from Glyph.";
    public const string DefaultSubject = "Glyph document";

    public static string ShareFailed(string message) => "Share failed: " + message;

    public static string CopyFileFailed(string message) => "Copy file failed: " + message;

    public static string OpenWithFailed(string message) => "Open With failed: " + message;

    public static string EmailFailed(string message) => "Email failed: " + message;

    public static string MailtoSubject(string? displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? DefaultSubject : displayName;
}
