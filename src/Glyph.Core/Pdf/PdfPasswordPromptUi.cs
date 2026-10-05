namespace Glyph.Core.Pdf;

/// <summary>
/// Password prompt copy for opening encrypted PDFs (F23-01).
/// </summary>
public static class PdfPasswordPromptUi
{
    public const string Title = "Password required";
    public const string RetryTitle = "Incorrect password";
    public const string Placeholder = "Password";
    public const string PrimaryButton = "Open";
    public const string CancelledStatus = "PDF open cancelled — password required.";
    public const string IncorrectStatus = "Incorrect PDF password.";
    public const string FailedStatus = "Could not open password-protected PDF.";
    public const int MaxAttempts = 3;

    public static string PromptBody(string fileName) =>
        $"Enter the password for “{fileName}”.";

    public static string DialogTitle(bool isRetry) => isRetry ? RetryTitle : Title;
}
