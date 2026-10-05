namespace Glyph.Core.Documents;

/// <summary>
/// "Select …" status prompts for annotation, bookmark, and attachment actions.
/// </summary>
public static class AnnotationSelectionStatus
{
    public const string AnnotationContextHint =
        "Select an annotation, then right-click for actions.";
    public const string BookmarkContextHint =
        "Select a bookmark, then right-click for actions.";
    public const string AttachmentContextHint =
        "Select an attachment, then right-click to save.";
    public const string SelectTextFirst = "Select text on the page first.";
    public const string CalloutTip = "Select a callout to move its tip.";
    public const string ShapeOrTextBoxFill =
        "Select a shape or text box to change fill.";
    public const string AnnotationColor = "Select an annotation to change color.";
    public const string GroupAtLeastTwo =
        "Select at least two annotations to group (Ctrl+click).";
    public const string Ungroup =
        "Select grouped annotation(s) to ungroup.";
    public const string Rotate = "Select an annotation to rotate.";
    public const string UnderlineTextBoxOrCallout =
        "Select a text box or callout to underline.";
    public const string AlignmentTextBoxOrCallout =
        "Select a text box or callout to set alignment.";
    public const string Duplicate = "Select an annotation to duplicate.";
    public const string InkOrShapeWidth =
        "Select an ink or shape annotation to change width.";
    public const string Opacity = "Select an annotation to change opacity.";
    public const string Delete = "Select an annotation to delete.";
    public const string RenameBookmark = "Select a bookmark to rename.";
    public const string DeleteBookmark = "Select a bookmark to delete.";
    public const string SaveAttachment = "Select an attachment to save.";
}
