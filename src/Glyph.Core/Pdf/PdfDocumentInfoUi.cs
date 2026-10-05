namespace Glyph.Core.Pdf;

/// <summary>
/// Document Info / Properties dialog labels (F48 / F23-08 / F25).
/// </summary>
public static class PdfDocumentInfoUi
{
    public const string DialogTitle = "Document info";
    public const string EditDialogTitle = "Edit document info";
    public const string EditButton = "Edit…";
    public const string SaveButton = "Save";
    public const string ClearAllButton = "Clear all";
    public const string CloseButton = "Close";
    public const string PropertiesUnavailablePrefix = "Properties unavailable: ";
    public const string EditCancelledStatus = "Info edit cancelled.";
    public const string ClearedStatus = "Document info cleared. Save the PDF to keep changes on disk.";
    public const string UpdatedStatus = "Document info updated. Save the PDF to keep changes on disk.";
    public const string UndidStatus = "Undid document info edit.";
    public const string FailedPrefix = "Info edit failed: ";

    public const string FieldTitle = "Title";
    public const string FieldAuthor = "Author";
    public const string FieldSubject = "Subject";
    public const string FieldKeywords = "Keywords";
    public const string FieldCreator = "Creator";
    public const string FieldProducer = "Producer";

    public static IReadOnlyList<string> EditableFieldLabels { get; } =
    [
        FieldTitle,
        FieldAuthor,
        FieldSubject,
        FieldKeywords,
        FieldCreator,
        FieldProducer,
    ];

    public static string FailedStatus(string message) => FailedPrefix + message;

    public static string FormatSidebarSummary(
        string title,
        string author,
        string subject,
        string creator,
        string producer,
        int pageCount,
        string pageSize,
        string fileName,
        string fileSize,
        string pdfVersion,
        string encryptedMarker,
        int embeddedAttachmentCount)
    {
        var text =
            $"{FieldTitle}: {title}\n"
            + $"{FieldAuthor}: {author}\n"
            + $"{FieldSubject}: {subject}\n"
            + $"{FieldCreator}: {creator}\n"
            + $"{FieldProducer}: {producer}\n"
            + $"Pages: {pageCount} · {pageSize}\n"
            + $"File: {fileName} · {fileSize}\n"
            + $"PDF: {pdfVersion}"
            + encryptedMarker;
        if (embeddedAttachmentCount > 0)
        {
            text += $"\nAttachments: {embeddedAttachmentCount}";
        }

        return text;
    }
}
