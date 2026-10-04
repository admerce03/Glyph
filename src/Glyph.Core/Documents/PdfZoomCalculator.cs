namespace Glyph.Core.Documents;

/// <summary>
/// Pure zoom/fit math for the PDF viewer. Kept in Core so WinUI and tests share one implementation.
/// </summary>
public static class PdfZoomCalculator
{
    public const double MinScale = 0.25;
    public const double MaxScale = 4.0;
    public const double ZoomStep = 1.25;

    public static double Clamp(double scale) => Math.Clamp(scale, MinScale, MaxScale);

    public static double ActualSize() => 1.0;

    public static double FitWidth(double viewportWidthDip, double pageWidthPoints, double horizontalPadding = 24)
    {
        if (pageWidthPoints <= 0)
        {
            return ActualSize();
        }

        var usable = Math.Max(1, viewportWidthDip - horizontalPadding);
        return Clamp(usable / pageWidthPoints);
    }

    public static double FitPage(
        double viewportWidthDip,
        double viewportHeightDip,
        double pageWidthPoints,
        double pageHeightPoints,
        double horizontalPadding = 24,
        double verticalPadding = 24)
    {
        if (pageWidthPoints <= 0 || pageHeightPoints <= 0)
        {
            return ActualSize();
        }

        var usableWidth = Math.Max(1, viewportWidthDip - horizontalPadding);
        var usableHeight = Math.Max(1, viewportHeightDip - verticalPadding);
        var scale = Math.Min(usableWidth / pageWidthPoints, usableHeight / pageHeightPoints);
        return Clamp(scale);
    }

    public static double ZoomIn(double current) => Clamp(current * ZoomStep);

    public static double ZoomOut(double current) => Clamp(current / ZoomStep);

    /// <summary>
    /// Ctrl+wheel / pinch-style step. Positive delta zooms in.
    /// </summary>
    public static double ApplyWheelZoom(double current, int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return Clamp(current);
        }

        return wheelDelta > 0 ? ZoomIn(current) : ZoomOut(current);
    }
}
