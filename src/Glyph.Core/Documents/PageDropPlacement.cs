namespace Glyph.Core.Documents;

/// <summary>
/// Pure helpers for thumbnail page-drop placement and captions (F11-04/05/06).
/// </summary>
public static class PageDropPlacement
{
    /// <summary>
    /// True when the pointer is in the lower half of the drop target (insert after).
    /// </summary>
    public static bool IsInsertAfter(double pointerY, double targetHeight)
    {
        if (targetHeight <= 0)
        {
            return true;
        }

        return pointerY > targetHeight / 2;
    }

    public static string Caption(bool hasStorageItems, bool hasTextPayload)
        => hasStorageItems && !hasTextPayload
            ? "Insert PDF pages"
            : "Move or copy pages here";

    /// <summary>
    /// Ctrl forces copy; Explorer PDF drops (storage without text payload) are copy-only.
    /// </summary>
    public static bool PreferCopyOnly(bool controlHeld, bool storageItemsWithoutText) =>
        controlHeld || storageItemsWithoutText;

    /// <summary>True when a drop package can insert/reorder pages (F11-04).</summary>
    public static bool AcceptsDrop(bool hasTextPayload, bool hasStorageItems) =>
        hasTextPayload || hasStorageItems;

    /// <summary>Explorer PDF file drop without in-app page text payload (insert-only).</summary>
    public static bool IsStorageInsertOnly(bool hasStorageItems, bool hasTextPayload) =>
        hasStorageItems && !hasTextPayload;

    /// <summary>Ctrl or storage insert forces copy-only drop semantics.</summary>
    public static bool PreferCopyOperation(bool controlHeld, bool hasStorageItems, bool hasTextPayload) =>
        PreferCopyOnly(controlHeld, IsStorageInsertOnly(hasStorageItems, hasTextPayload));

    /// <summary>
    /// Border thickness for the orange insert indicator (left, top, right, bottom).
    /// Emphasizes the edge where pages will land.
    /// </summary>
    public static (double Left, double Top, double Right, double Bottom) HighlightThickness(bool insertAfter) =>
        insertAfter ? (2, 2, 2, 5) : (2, 5, 2, 2);
}
