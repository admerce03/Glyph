namespace Glyph.Core.Documents;

/// <summary>
/// Length units for numeric PDF crop UI. Stored operations remain in PDF points.
/// </summary>
public enum PdfLengthUnit
{
    Points,
    Inches,
    Centimeters,
    Millimeters,
}

public static class PdfLengthUnits
{
    private const double PointsPerInch = 72.0;
    private const double MmPerInch = 25.4;

    public static double ToPoints(double value, PdfLengthUnit unit) => unit switch
    {
        PdfLengthUnit.Points => value,
        PdfLengthUnit.Inches => value * PointsPerInch,
        PdfLengthUnit.Centimeters => value * PointsPerInch / 2.54,
        PdfLengthUnit.Millimeters => value * PointsPerInch / MmPerInch,
        _ => throw new ArgumentOutOfRangeException(nameof(unit)),
    };

    public static double FromPoints(double points, PdfLengthUnit unit) => unit switch
    {
        PdfLengthUnit.Points => points,
        PdfLengthUnit.Inches => points / PointsPerInch,
        PdfLengthUnit.Centimeters => points * 2.54 / PointsPerInch,
        PdfLengthUnit.Millimeters => points * MmPerInch / PointsPerInch,
        _ => throw new ArgumentOutOfRangeException(nameof(unit)),
    };

    public static string Abbreviation(PdfLengthUnit unit) => unit switch
    {
        PdfLengthUnit.Points => "pt",
        PdfLengthUnit.Inches => "in",
        PdfLengthUnit.Centimeters => "cm",
        PdfLengthUnit.Millimeters => "mm",
        _ => "pt",
    };
}
