namespace Glyph.Core.Documents;

/// <summary>
/// Status strings for annotation drawing tool modes (highlight/ink/eraser/callout/freeform/polygon/shape/magnifier).
/// </summary>
public static class AnnotationToolModeStatus
{
    public const string HighlightOff = "Highlight mode off.";
    public const string HighlightCancelled = "Highlight mode cancelled.";
    public const string HighlightOn =
        "Highlight mode on — select text to highlight (Esc to exit).";
    public const string HighlightMarkCancelled = "Highlight cancelled.";

    public const string InkOff = "Ink mode off.";
    public const string InkCancelled = "Ink mode cancelled.";
    public const string InkOn = "Ink mode on — draw on the page.";

    public const string EraserOff = "Eraser off.";
    public const string EraserOn =
        "Eraser on — click an annotation to remove it (Esc to exit).";
    public const string EraserMiss = "Eraser: no annotation under cursor.";

    public const string CalloutOff = "Callout mode off.";
    public const string CalloutOn =
        "Callout mode — drag from tip to where the text box should sit.";
    public const string CalloutCancelled = "Callout cancelled.";
    public const string CalloutAdded = "Callout added.";
    public const string CalloutTipPrompt =
        "Callout tip — click on the page where the pointer should point.";
    public const string CalloutTipEditCancelled = "Callout tip edit cancelled.";
    public const string CalloutTipWrongPage = "Callout tip edit cancelled (wrong page).";
    public const string CalloutTipMoved = "Callout tip moved.";

    public const string FreeformOff = "Freeform mode off.";
    public const string FreeformCancelled = "Freeform mode cancelled.";
    public const string FreeformOn = "Freeform mode on — draw a closed shape.";

    public const string PolygonOff = "Polygon mode off.";
    public const string PolygonCancelled = "Polygon cancelled.";
    public const string PolygonModeCancelled = "Polygon mode cancelled.";
    public const string PolygonOn =
        "Polygon mode — click vertices; Enter to close (Esc cancels).";
    public const string PolygonNeedsVertices = "Polygon needs at least three vertices.";
    public const string PolygonAdded = "Polygon added.";

    public const string ShapeOff = "Shape mode off.";
    public const string ShapeCancelled = "Shape mode cancelled.";

    public const string MagnifierOn =
        "Magnifier on — move over a page (Esc to exit).";
    public const string MagnifierOff = "Magnifier off.";

    public const string CropOn =
        "Crop mode — drag handles, Enter to apply, Esc to cancel.";

    public const string ExitedPresentation = "Exited presentation mode.";
}
