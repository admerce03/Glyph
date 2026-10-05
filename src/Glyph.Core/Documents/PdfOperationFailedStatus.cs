namespace Glyph.Core.Documents;

/// <summary>
/// Shared "… failed: " status prefixes for PDF document operations.
/// </summary>
public static class PdfOperationFailedStatus
{
    public const string EditProperties = "Edit properties failed: ";
    public const string Drop = "Drop failed: ";
    public const string CopyPages = "Copy pages failed: ";
    public const string Markup = "Markup failed: ";
    public const string CalloutTip = "Callout tip failed: ";
    public const string Fill = "Fill failed: ";
    public const string Color = "Color failed: ";
    public const string AnnotationList = "Annotation list failed: ";
    public const string Callout = "Callout failed: ";
    public const string Eraser = "Eraser failed: ";
    public const string Polygon = "Polygon failed: ";
    public const string Shape = "Shape failed: ";
    public const string FreeformOrInk = " failed: "; // prefixed with Freeform/Ink
    public const string Cleanup = "Cleanup failed: ";
    public const string UndoAnnotation = "Undo annotation failed: ";
    public const string UndoFormFill = "Undo form fill failed: ";
    public const string Reorder = "Reorder failed: ";
    public const string Delete = "Delete failed: ";
    public const string DescriptionSave = "Description save failed: ";
    public const string InsertSignature = "Insert signature failed: ";
    public const string Signature = "Signature failed: ";
    public const string CameraInsert = "Camera insert failed: ";
    public const string WebcamSignature = "Webcam signature failed: ";
    public const string ButtonLink = "Button link failed: ";
    public const string CouldNotOpenSignatureLibrary = "Could not open signature library: ";
    public const string FormSignature = "Form signature failed: ";
    public const string FormFill = "Form fill failed: ";
    public const string Flatten = "Flatten failed: ";
    public const string Note = "Note failed: ";
    public const string Group = "Group failed: ";
    public const string Ungroup = "Ungroup failed: ";
    public const string Move = "Move failed: ";
    public const string EndpointAdjust = "Endpoint adjust failed: ";
    public const string Resize = "Resize failed: ";
    public const string CouldNotListNotes = "Could not list notes: ";
    public const string Rotate = "Rotate failed: ";
    public const string Underline = "Underline failed: ";
    public const string Alignment = "Alignment failed: ";
    public const string EditAnnotation = "Edit annotation failed: ";
    public const string DuplicateAnnotation = "Duplicate annotation failed: ";
    public const string Width = "Width failed: ";
    public const string Opacity = "Opacity failed: ";
    public const string DeleteAnnotation = "Delete annotation failed: ";
    public const string UndoInfoEdit = "Undo info edit failed: ";
    public const string Crop = "Crop failed: ";
    public const string Merge = "Merge failed: ";
    public const string Export = "Export failed: ";
    public const string OutlineExport = "Outline export failed: ";
    public const string Print = "Print failed: ";
    public const string Info = "Info failed: ";
    public const string Save = "Save failed: ";
    public const string AttachmentSave = "Attachment save failed: ";

    public static string Format(string prefix, string message) => prefix + message;

    public static string FormatFreeformOrInk(bool freeformMode, string message) =>
        (freeformMode ? "Freeform" : "Ink") + FreeformOrInk + message;
}
