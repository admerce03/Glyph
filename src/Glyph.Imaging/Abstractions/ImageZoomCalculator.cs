namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Pure zoom/fit/decode-edge math for the image viewer (F26-11/12/14/15/22).
/// </summary>
public static class ImageZoomCalculator
{
    public const double MinScale = 0.05;
    public const double MaxScale = 8.0;
    public const double ZoomStep = 1.25;
    public const double WheelStep = 1.1;
    public const int MinDecodeEdge = 256;
    public const int MaxDecodeEdge = 8192;

    public static double Clamp(double scale) => Math.Clamp(scale, MinScale, MaxScale);

    public static double ActualSizePixels() => 1.0;

    /// <summary>
    /// Print-meaning 100%: scale so image DPI matches screen DPI.
    /// </summary>
    public static double ActualSizePrint(double screenDpi, double imageDpi) =>
        Clamp(screenDpi / Math.Max(1.0, imageDpi));

    public static double Fit(
        double viewportWidthDip,
        double viewportHeightDip,
        int pixelWidth,
        int pixelHeight,
        double padding = 24)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            return ActualSizePixels();
        }

        var usableW = Math.Max(1, viewportWidthDip - padding);
        var usableH = Math.Max(1, viewportHeightDip - padding);
        return Clamp(Math.Min(usableW / pixelWidth, usableH / pixelHeight));
    }

    public static double ZoomIn(double current) => Clamp(current * ZoomStep);

    public static double ZoomOut(double current) => Clamp(current / ZoomStep);

    /// <summary>
    /// Ctrl+wheel step. Positive delta zooms in (×1.1).
    /// </summary>
    public static double ApplyWheelZoom(double current, int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return Clamp(current);
        }

        var factor = wheelDelta > 0 ? WheelStep : 1.0 / WheelStep;
        return Clamp(current * factor);
    }

    public static double ApplyManipulationScale(double current, double deltaScale) =>
        Clamp(current * deltaScale);

    /// <summary>
    /// Progressive decode target edge length for the current zoom (F26-11/F58-02).
    /// </summary>
    public static int DecodeTargetEdge(int nativeMaxEdge, double zoom) =>
        (int)Math.Clamp(Math.Max(0, nativeMaxEdge) * zoom, MinDecodeEdge, MaxDecodeEdge);
}
