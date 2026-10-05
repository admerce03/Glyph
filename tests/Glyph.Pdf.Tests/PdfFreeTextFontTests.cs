using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfFreeTextFontTests
{
    [Theory]
    [InlineData(PdfFreeTextFontFamily.Helvetica, false, false, "Helv")]
    [InlineData(PdfFreeTextFontFamily.Helvetica, true, false, "HeBo")]
    [InlineData(PdfFreeTextFontFamily.Helvetica, false, true, "HeOb")]
    [InlineData(PdfFreeTextFontFamily.Helvetica, true, true, "HeBI")]
    [InlineData(PdfFreeTextFontFamily.Times, false, false, "TiRo")]
    [InlineData(PdfFreeTextFontFamily.Times, true, true, "TiBI")]
    [InlineData(PdfFreeTextFontFamily.Courier, true, false, "CoBo")]
    [InlineData(PdfFreeTextFontFamily.Courier, false, true, "CoOb")]
    public void ResolveResourceName_maps_standard_faces(
        PdfFreeTextFontFamily family,
        bool bold,
        bool italic,
        string expected)
    {
        PdfFreeTextFont.ResolveResourceName(family, bold, italic).Should().Be(expected);
    }
}
