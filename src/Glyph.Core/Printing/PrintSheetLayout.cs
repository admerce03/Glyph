namespace Glyph.Core.Printing;

/// <summary>
/// Print scale modes for fitting content into a printable cell (F44-13–16).
/// </summary>
public enum PrintScaleMode
{
    Fit = 0,
    Fill = 1,
    ActualSize = 2,
}

/// <summary>
/// Sized content placement for one print cell, including optional 90° auto-rotate.
/// </summary>
public readonly record struct PrintTargetLayout(
    bool Rotate,
    double ImageWidth,
    double ImageHeight,
    double OccupiedWidth,
    double OccupiedHeight);

/// <summary>
/// Pure N-up cell geometry and scale/rotate math for print preview (F44-13–19).
/// </summary>
public static class PrintSheetLayout
{
    public const double TwoUpGap = 12;
    public const double FourUpGap = 10;

    /// <summary>Clamp to supported pages-per-sheet values (1, 2, or 4).</summary>
    public static int NormalizePagesPerSheet(int pagesPerSheet) =>
        pagesPerSheet is 2 or 4 ? pagesPerSheet : 1;

    /// <summary>
    /// Sheet cells for 1-/2-/4-up layout within a printable rectangle.
    /// Origin is top-left; Y grows downward (UI/DIP space).
    /// </summary>
    public static IReadOnlyList<PrintCell> Cells(double pageWidth, double pageHeight, int pagesPerSheet)
    {
        var n = NormalizePagesPerSheet(pagesPerSheet);
        if (n <= 1)
        {
            return [new PrintCell(0, 0, pageWidth, pageHeight)];
        }

        if (n == 2)
        {
            var cellW = (pageWidth - TwoUpGap) / 2;
            return
            [
                new PrintCell(0, 0, cellW, pageHeight),
                new PrintCell(cellW + TwoUpGap, 0, cellW, pageHeight),
            ];
        }

        var gap = FourUpGap;
        var w = (pageWidth - gap) / 2;
        var h = (pageHeight - gap) / 2;
        return
        [
            new PrintCell(0, 0, w, h),
            new PrintCell(w + gap, 0, w, h),
            new PrintCell(0, h + gap, w, h),
            new PrintCell(w + gap, h + gap, w, h),
        ];
    }

    /// <summary>
    /// Number of printable sheets needed for <paramref name="pageCount"/> source pages.
    /// </summary>
    public static int SheetCount(int pageCount, int pagesPerSheet)
    {
        if (pageCount <= 0)
        {
            return 0;
        }

        var n = NormalizePagesPerSheet(pagesPerSheet);
        return (pageCount + n - 1) / n;
    }

    /// <summary>
    /// True when content aspect and cell aspect disagree enough that a 90° rotate helps.
    /// </summary>
    public static bool ShouldAutoRotate(double contentW, double contentH, double availW, double availH) =>
        (contentW > contentH && availW < availH)
        || (contentH > contentW && availH < availW);

    /// <summary>
    /// Target image size and occupied cell size (post optional 90° host swap).
    /// </summary>
    public static PrintTargetLayout ComputeTarget(
        double contentW,
        double contentH,
        double availW,
        double availH,
        PrintScaleMode scaleMode,
        bool autoRotate)
    {
        var rotate = autoRotate && ShouldAutoRotate(contentW, contentH, availW, availH);
        var layoutW = rotate ? availH : availW;
        var layoutH = rotate ? availW : availH;

        double imageW;
        double imageH;
        switch (scaleMode)
        {
            case PrintScaleMode.ActualSize:
                imageW = Math.Min(contentW, layoutW);
                imageH = Math.Min(contentH, layoutH);
                break;
            case PrintScaleMode.Fill:
                imageW = layoutW;
                imageH = layoutH;
                break;
            default:
                var scale = Math.Min(
                    layoutW / Math.Max(1, contentW),
                    layoutH / Math.Max(1, contentH));
                imageW = contentW * scale;
                imageH = contentH * scale;
                break;
        }

        if (rotate)
        {
            // 90° host: width/height of the occupied region swap.
            return new PrintTargetLayout(true, imageW, imageH, imageH, imageW);
        }

        return new PrintTargetLayout(false, imageW, imageH, imageW, imageH);
    }

    /// <summary>Offset to place a sized target inside a cell, optionally centered.</summary>
    public static (double X, double Y) PlaceInCell(
        double cellLeft,
        double cellTop,
        double cellW,
        double cellH,
        double targetW,
        double targetH,
        bool center) =>
        (
            cellLeft + (center ? Math.Max(0, (cellW - targetW) / 2) : 0),
            cellTop + (center ? Math.Max(0, (cellH - targetH) / 2) : 0));
}

/// <summary>One cell on a multi-up print sheet (UI/DIP, Y-down).</summary>
public readonly record struct PrintCell(double Left, double Top, double Width, double Height);
