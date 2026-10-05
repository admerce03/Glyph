using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PdfPageSizeFormatTests
{
    [Fact]
    public void FormatPoints_null_uses_label()
    {
        PdfPageSizeFormat.FormatPoints(null, 100).Should().Be("—");
        PdfPageSizeFormat.FormatPoints(100, null).Should().Be("—");
        PdfPageSizeFormat.FormatPoints(null, null, nullLabel: "?").Should().Be("?");
    }

    [Fact]
    public void FormatPoints_formats_dimensions()
    {
        PdfPageSizeFormat.FormatPoints(612, 792).Should().Be("612 × 792 pt");
        PdfPageSizeFormat.FormatPoints(612.25, 792.5, separator: "×")
            .Should().Be("612.3×792.5 pt");
    }
}
