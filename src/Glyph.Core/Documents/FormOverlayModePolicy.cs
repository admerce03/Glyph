namespace Glyph.Core.Documents;

/// <summary>
/// AcroForm overlay mode keyboard/status (F20-11).
/// </summary>
public static class FormOverlayModePolicy
{
    public const string NoFields = "AcroForm present but no widget fields found.";
    public const string NoAcroFormFields = "No AcroForm fields in this document.";
    public const string NoFieldFocused = "No form field focused.";
    public const string EditorClosed = "Form editor closed.";
    public const string NoChangeToUndo = "No form change to undo.";
    public const string UndidFieldChange = "Undid form field change.";
    public const string FlattenCancelled = "Flatten cancelled.";
    public const string Flattening = "Flattening…";
    public const string NothingToFlatten = "Nothing to flatten.";
    public const string Started =
        "Form overlay on — click a field, Tab/Shift+Tab to move, Enter to edit, Esc to exit.";
    public const string Exited = "Form overlay off.";

    public static string FormatSignedField(string fieldName, string signatureName) =>
        $"Signed form field '{fieldName}' with '{signatureName}'.";

    public static int NextFocusIndex(int current, int count, bool forward)
    {
        if (count <= 0)
        {
            return -1;
        }

        if (current < 0 || current >= count)
        {
            return 0;
        }

        return forward
            ? (current + 1) % count
            : (current - 1 + count) % count;
    }
}
