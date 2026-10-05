using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfInkLineStyleDashPatternTests
{
    [Fact]
    public void Solid_is_null()
    {
        PdfInkLineStyleDashPattern.ForPreview(PdfInkLineStyle.Solid).Should().BeNull();
    }

    [Theory]
    [InlineData(PdfInkLineStyle.Dashed, 6, 4)]
    [InlineData(PdfInkLineStyle.Dotted, 1.5, 4)]
    public void Styled_lines_have_dash_gap(PdfInkLineStyle style, double dash, double gap)
    {
        var pattern = PdfInkLineStyleDashPattern.ForPreview(style);
        pattern.Should().NotBeNull();
        pattern!.Should().Equal(dash, gap);
    }
}
