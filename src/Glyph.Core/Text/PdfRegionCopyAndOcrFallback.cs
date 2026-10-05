namespace Glyph.Core.Text;

/// <summary>
/// Rectangular region-as-image copy thresholds (F07-11/12).
/// </summary>
public static class PdfRegionCopyPolicy
{
    public const double MinDisplayEdge = 4;
    public const string DragRegionFirst = "Drag a region on the page first.";
    public const string Copying = "Copying region…";
    public const string FailedPrefix = "Copy region failed: ";

    public static bool HasValidRegion(int pageIndex, double width, double height) =>
        pageIndex >= 0 && width >= MinDisplayEdge && height >= MinDisplayEdge;

    public static string FormatCopied(int width, int height) =>
        $"Copied region ({width}×{height}) to clipboard.";

    public static string FormatFailed(string message) => FailedPrefix + message;
}

/// <summary>
/// Page CanDrag exports selected text (F07-13).
/// </summary>
public static class PdfTextDragPolicy
{
    public static bool CanStartTextDrag(string? selectedText) =>
        !string.IsNullOrWhiteSpace(selectedText);

    public const string DraggingStatus = "Dragging selected text…";
}

/// <summary>
/// Find offers OCR when the PDF has no extractable text (F07-09).
/// </summary>
public static class FindOcrFallbackPolicy
{
    public static bool ShouldOfferOcr(string? searchMessage) =>
        !string.IsNullOrWhiteSpace(searchMessage)
        && searchMessage.Contains("OCR", StringComparison.OrdinalIgnoreCase);

    public const string DialogTitle = "OCR required";
    public const string DialogMessage =
        "This PDF has no extractable text. Run OCR on the current page so Find can search recognized text?";
    public const string PrimaryButton = "OCR page";
}

/// <summary>
/// Zoom-to-area mode status (F02-22).
/// </summary>
public static class PdfZoomAreaStatus
{
    public const string Prompt =
        "Zoom area — drag a rectangle on a page (Esc to cancel).";
    public const string CancelledTooSmall =
        "Zoom area cancelled — drag a larger rectangle.";

    public static string FormatZoomed(double scale) =>
        $"Zoomed to {scale * 100:0}%.";

    public static string FormatZoomedToArea(double scale) =>
        $"Zoomed to area at {scale * 100:0}%.";
}
