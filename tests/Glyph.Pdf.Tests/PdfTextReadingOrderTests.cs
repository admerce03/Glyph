using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Text;

namespace Glyph.Pdf.Tests;

public class PdfTextReadingOrderTests
{
    [Fact]
    public void IsNewLine_when_vertical_midpoints_differ_enough()
    {
        var top = new PdfRect(0, 20, 10, 30);
        var bottom = new PdfRect(0, 0, 10, 10);
        PdfTextReadingOrder.IsNewLine(top, bottom).Should().BeTrue();
        PdfTextReadingOrder.IsNewLine(top, new PdfRect(10, 19, 20, 29)).Should().BeFalse();
    }

    [Fact]
    public void NeedsSpaceBetween_when_horizontal_gap_is_large()
    {
        var hello = new PdfTextChar(0, "Hello", new PdfRect(0, 0, 40, 10));
        var world = new PdfTextChar(1, "world", new PdfRect(55, 0, 100, 10));
        PdfTextReadingOrder.NeedsSpaceBetween(hello, world).Should().BeTrue();
    }

    [Fact]
    public void NeedsSpaceBetween_false_when_adjacent_glyphs()
    {
        var a = new PdfTextChar(0, "A", new PdfRect(0, 0, 10, 10));
        var b = new PdfTextChar(1, "B", new PdfRect(10, 0, 20, 10));
        PdfTextReadingOrder.NeedsSpaceBetween(a, b).Should().BeFalse();
    }
}
