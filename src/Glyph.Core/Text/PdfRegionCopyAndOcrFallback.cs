namespace Glyph.Core.Text;

/// <summary>
/// Rectangular region-as-image copy thresholds (F07-11/12).
/// </summary>
public static class PdfRegionCopyPolicy
{
    public const double MinDisplayEdge = 4;

    public static bool HasValidRegion(int pageIndex, double width, double height) =>
        pageIndex >= 0 && width >= MinDisplayEdge && height >= MinDisplayEdge;
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
