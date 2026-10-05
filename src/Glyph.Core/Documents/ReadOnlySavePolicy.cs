namespace Glyph.Core.Documents;

/// <summary>
/// Save vs Save As when the target path is read-only (F01-13).
/// </summary>
public static class ReadOnlySavePolicy
{
    public static bool RequiresSaveAs(bool sessionIsReadOnly, bool pathIsReadOnly) =>
        sessionIsReadOnly || pathIsReadOnly;

    public const string DialogTitle = "Read-only file";

    public const string DialogMessage =
        "This file is read-only. Use Save As… to write a writable copy, or remove the read-only attribute in Explorer.";

    public const string CancelledStatus = "Save cancelled — file is read-only.";
}
