namespace Glyph.Core.Documents;

/// <summary>
/// Exception messages when App.CurrentApp.MainWindowInstance is null.
/// </summary>
public static class MainWindowRequiredMessages
{
    public const string Prefix = "Main window unavailable for ";

    public const string AlignmentDialog = "Main window unavailable for alignment dialog.";
    public const string AttachmentSave = "Main window unavailable for attachment save.";
    public const string AuthorDialog = "Main window unavailable for author dialog.";
    public const string ButtonDialog = "Main window unavailable for button dialog.";
    public const string CalloutDialog = "Main window unavailable for callout dialog.";
    public const string Camera = "Main window unavailable for camera.";
    public const string ColorDialog = "Main window unavailable for color dialog.";
    public const string DocumentInfoEdit = "Main window unavailable for document info edit.";
    public const string DocumentInfo = "Main window unavailable for document info.";
    public const string EditDialog = "Main window unavailable for edit dialog.";
    public const string Export = "Main window unavailable for export.";
    public const string FillDialog = "Main window unavailable for fill dialog.";
    public const string FlattenDialog = "Main window unavailable for flatten dialog.";
    public const string FolderPicker = "Main window unavailable for folder picker.";
    public const string FormDialog = "Main window unavailable for form dialog.";
    public const string NoteDialog = "Main window unavailable for note dialog.";
    public const string OpacityDialog = "Main window unavailable for opacity dialog.";
    public const string OpenPicker = "Main window unavailable for open picker.";
    public const string Optimize = "Main window unavailable for optimize.";
    public const string Print = "Main window unavailable for print.";
    public const string ProfileDialog = "Main window unavailable for profile dialog.";
    public const string RedactionApply = "Main window unavailable for redaction apply.";
    public const string RedactionDialog = "Main window unavailable for redaction dialog.";
    public const string SavePicker = "Main window unavailable for save picker.";
    public const string SignatureDialog = "Main window unavailable for signature dialog.";
    public const string SignatureName = "Main window unavailable for signature name.";
    public const string StrokeStyleDialog = "Main window unavailable for stroke style dialog.";
    public const string TextBoxDialog = "Main window unavailable for text box dialog.";
    public const string WidthDialog = "Main window unavailable for width dialog.";
    public const string SignatureImageEmpty = "Signature image is empty.";

    public static string For(string purpose) => Prefix + purpose + ".";
}
