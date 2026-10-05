namespace Glyph.Core.Documents;

/// <summary>
/// File → Save status strings (F01-16).
/// </summary>
public static class DocumentSaveStatus
{
    public const string NoDocument = "Open a document to save.";

    public static string Saved(string displayName, bool isReadOnly) =>
        "Saved " + displayName + (isReadOnly ? " · read-only" : string.Empty);
}
