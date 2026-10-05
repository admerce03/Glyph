using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfStrokeWidthPresetsTests
{
    [Fact]
    public void Labels_and_nearest_index()
    {
        PdfStrokeWidthPresets.Points.Should().Equal(1f, 2f, 3f, 5f, 8f);
        PdfStrokeWidthPresets.FormatLabel(2f).Should().Be("2 pt");
        PdfStrokeWidthPresets.IndexOfNearest(5f).Should().Be(3);
        PdfStrokeWidthPresets.DefaultSelectedIndex(2f).Should().Be(1);
        PdfStrokeWidthPresets.DefaultSelectedIndex(9f).Should().Be(1);
    }
}
