namespace Glyph.Core.Text;

/// <summary>
/// Page text / region context-menu labels and copy status (F07-02/06/07/11).
/// </summary>
public static class PdfTextInteractionUi
{
    public const string SelectAllText = "Select all text";
    public const string SelectAllTextInDocument = "Select all text in document";
    public const string Copy = "Copy";
    public const string FindSelection = "Find selection";
    public const string SearchWeb = "Search web";
    public const string CopyRegionAsImage = "Copy region as image";
    public const string NoExtractableText = "No extractable text on this page.";

    public static string CopiedCharacters(int count) => $"Copied {count} characters.";
}
