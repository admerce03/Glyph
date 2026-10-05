namespace Glyph.Core.Signatures;

/// <summary>
/// Signature library / stamp UI labels (F19 / F34-06 / F55-20).
/// </summary>
public static class SignatureLibraryUi
{
    public const string ToolbarStamp = "Stamp";
    public const string LibraryTitle = "Signatures";
    public const string ClearLibraryPrefsNote = "Preferences can clear the signature library.";
    public const bool ImageMarkupUsesPasteFile = true;
    public const bool SupportsSaveDeleteReorderDescriptions = true;

    public const string DrawCancelled = "Signature draw cancelled.";
    public const string Cancelled = "Signature cancelled.";
    public const string OrderUpdated = "Signature order updated.";
    public const string DescriptionSaved = "Signature description saved.";
    public const string SelectToInsert = "Select a signature to insert.";
    public const string Inserting = "Inserting signature…";
    public const string ImageEmpty = "Signature image is empty.";
    public const string DrawPrompt =
        "Signature draw — draw on the page, then release to save.";
    public const string StrokeTooShort = "Signature stroke too short.";
    public const string Saving = "Saving signature…";
    public const string Inserted = "Signature inserted.";
    public const string WebcamInserted = "Webcam signature inserted.";
    public const string WebcamPaperSignature = "Webcam paper signature";
    public const string PhotographSignatureTitle = "Photograph signature";
    public const string PhotographSignatureHint =
        "Point the camera at a signature on paper, then Capture. Near-white paper is keyed out.";
    public const string Loading = "Loading signature…";
    public const string WebcamCancelled = "Webcam signature cancelled.";
    public const string DrawThenChooseFormField =
        "Draw a signature, then choose the form field again to insert it.";
    public const string SigningFormField = "Signing form field…";

    public static string FormatDeleted(string name) =>
        $"Deleted signature '{name}'.";

    public static string FormatInserted(string name) =>
        $"Inserted signature '{name}'.";

    public static string FormatImageContents(string fileName) =>
        $"Signature image {fileName}";

    public static string FormatWebcamName(DateTime when) =>
        $"Webcam {when:yyyy-MM-dd HH:mm}";
}
