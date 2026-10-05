using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Pdf.Abstractions;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfCropMarginsParserTests
{
    [Fact]
    public void FromDialogValues_converts_inches()
    {
        var margins = PdfCropMarginsParser.FromDialogValues(1, 0.5, 1, 0.5, PdfLengthUnit.Inches);
        margins.LeftPoints.Should().Be(72);
        margins.TopPoints.Should().Be(36);
        margins.RightPoints.Should().Be(72);
        margins.BottomPoints.Should().Be(36);
    }

    [Fact]
    public void FromDialogValues_nan_and_negative_become_zero()
    {
        var margins = PdfCropMarginsParser.FromDialogValues(
            double.NaN,
            -2,
            double.PositiveInfinity,
            3,
            PdfLengthUnit.Points);
        margins.LeftPoints.Should().Be(0);
        margins.TopPoints.Should().Be(0);
        margins.RightPoints.Should().Be(0);
        margins.BottomPoints.Should().Be(3);
    }
}
