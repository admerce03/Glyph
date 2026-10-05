namespace Glyph.Core.Documents;

/// <summary>
/// File → Save status strings (F01-16).
/// </summary>
public static class DocumentSaveStatus
{
    public const string NoDocument = "Open a document to save.";
    public const string Cancelled = "Save cancelled.";

    public static string Saved(string displayName, bool isReadOnly) =>
        "Saved " + displayName + (isReadOnly ? " · read-only" : string.Empty);

    public static string SavedFileName(string fileName) => "Saved " + fileName;
}
