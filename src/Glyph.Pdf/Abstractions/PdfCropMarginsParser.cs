using Glyph.Core.Documents;

namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Builds <see cref="PdfCropMargins"/> from numeric crop dialog values (F12-03).
/// </summary>
public static class PdfCropMarginsParser
{
    /// <summary>
    /// Converts L/T/R/B dialog values in <paramref name="unit"/> to non-negative point margins.
    /// Empty/NaN NumberBox values become 0.
    /// </summary>
    public static PdfCropMargins FromDialogValues(
        double left,
        double top,
        double right,
        double bottom,
        PdfLengthUnit unit) =>
        new(
            SanitizeToPoints(left, unit),
            SanitizeToPoints(top, unit),
            SanitizeToPoints(right, unit),
            SanitizeToPoints(bottom, unit));

    private static double SanitizeToPoints(double value, PdfLengthUnit unit)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            value = 0;
        }

        return Math.Max(0, PdfLengthUnits.ToPoints(value, unit));
    }
}
