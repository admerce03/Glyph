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
}
