using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PdfLengthUnitsTests
{
    [Fact]
    public void Inch_round_trips_to_points()
    {
        PdfLengthUnits.ToPoints(1, PdfLengthUnit.Inches).Should().Be(72);
        PdfLengthUnits.FromPoints(72, PdfLengthUnit.Inches).Should().BeApproximately(1, 1e-9);
    }

    [Fact]
    public void Millimeter_and_centimeter_conversions()
    {
        PdfLengthUnits.ToPoints(25.4, PdfLengthUnit.Millimeters).Should().BeApproximately(72, 1e-9);
        PdfLengthUnits.ToPoints(2.54, PdfLengthUnit.Centimeters).Should().BeApproximately(72, 1e-9);
    }
}
