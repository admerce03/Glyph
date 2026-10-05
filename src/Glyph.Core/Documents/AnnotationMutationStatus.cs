namespace Glyph.Core.Documents;

/// <summary>
/// Status after mutating PDF annotations (color/size/rotate/author/edit/erase).
/// </summary>
public static class AnnotationMutationStatus
{
    public const string ColorUpdated = "Annotation color updated.";
    public const string Resized = "Annotation resized.";
    public const string Rotated90 = "Annotation rotated 90°.";
    public const string EditCancelled = "Edit cancelled.";

    public static string FormatAuthorSet(string author) =>
        $"Annotation author set to {author}.";

    public static string FormatErased(string label) =>
        $"Erased {label}.";

    public static string FormatUndid(string label) =>
        $"Undid {label}.";

    public static string FormatUpdated(string label) =>
        $"Updated {label}.";

    public static string FormatDuplicated(string label) =>
        $"Duplicated {label}.";
}
