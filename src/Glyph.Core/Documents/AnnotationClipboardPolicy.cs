namespace Glyph.Core.Documents;

/// <summary>
/// In-app annotation cut/copy/paste clipboard semantics (F13-24/25/26).
/// </summary>
public static class AnnotationClipboardPolicy
{
    public const string SelectToCopy = "Select an annotation to copy.";
    public const string SelectToCut = "Select an annotation to cut.";
    public const string EmptyClipboard = "Annotation clipboard is empty.";

    public static string Copied(string label) => $"Copied {label}.";

    public static string Cut(string label) => $"Cut {label} (removed on paste).";

    public static string Pasted(string label, bool wasCut) =>
        wasCut ? $"Pasted {label} (cut)." : $"Pasted {label}.";

    public static string PasteFailed(string message) => "Paste annotation failed: " + message;

    /// <summary>
    /// After removing the cut source, adjust the pasted annot index when both share a page
    /// and the removed index was below the duplicate.
    /// </summary>
    public static int AdjustIndexAfterCutRemove(int sourcePage, int sourceIndex, int copyPage, int copyIndex) =>
        sourcePage == copyPage && sourceIndex < copyIndex ? copyIndex - 1 : copyIndex;

    public static bool ClearClipboardAfterPaste(bool wasCut) => wasCut;
}
