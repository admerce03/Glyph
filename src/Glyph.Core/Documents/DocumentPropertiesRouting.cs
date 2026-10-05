namespace Glyph.Core.Documents;

/// <summary>
/// Routes File → Properties to PDF Info vs image Meta (F01-21).
/// </summary>
public enum DocumentPropertiesKind
{
    None,
    PdfInfo,
    ImageMetadata,
}

public static class DocumentPropertiesRouting
{
    public static DocumentPropertiesKind For(DocumentKind kind) =>
        kind switch
        {
            DocumentKind.Pdf => DocumentPropertiesKind.PdfInfo,
            DocumentKind.Image => DocumentPropertiesKind.ImageMetadata,
            _ => DocumentPropertiesKind.None,
        };

    public const string NoDocumentStatus = "Open a document to view properties.";
}
