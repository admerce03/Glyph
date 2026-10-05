namespace Glyph.Core.Documents;

/// <summary>
/// FreeText text-box dialog status (F16-02).
/// </summary>
public static class PdfTextBoxDialogStatus
{
    public const string Title = "Text box";
    public const string Placeholder = "Text box contents";
    public const string Cancelled = "Text box cancelled.";
    public const string Adding = "Adding text box…";
    public const string Added = "Text box added.";

    public static string Failed(string message) => "Text box failed: " + message;
}
