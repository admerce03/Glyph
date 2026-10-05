namespace Glyph.Core.Documents;

/// <summary>
/// Progress/done status while persisting or moving PDF annotations.
/// </summary>
public static class AnnotationPersistStatus
{
    public const string SavingInk = "Saving ink…";
    public const string SavingShape = "Saving shape…";
    public const string SavingFreeform = "Saving freeform…";
    public const string SavingPolygon = "Saving polygon…";
    public const string MovingAnnotation = "Moving annotation…";
    public const string MovingCalloutTip = "Moving callout tip…";
    public const string FreeformShapeAdded = "Freeform shape added.";
    public const string FillAppliesHint =
        "Fill applies to rectangles, ellipses, loupes, and text boxes.";

    public static string FormatMovingAnnotations(int count) =>
        $"Moving {count} annotations…";
}

/// <summary>
/// Shared undo/redo progress status for document mutations.
/// </summary>
public static class DocumentUndoRedoStatus
{
    public const string NothingToUndo = "Nothing to undo.";
    public const string NothingToRedo = "Nothing to redo.";
    public const string Undoing = "Undoing…";
    public const string Redoing = "Redoing…";
}
