using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Text;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfTextSelectionReadingOrderTests
{
    [Fact]
    public void CopyCharsInRect_orders_top_to_bottom_with_newlines()
    {
        // PDF Y grows upward: line 2 is below line 1.
        var chars = new List<PdfTextChar>
        {
            new(0, "A", new PdfRect(0, 20, 10, 30)),
            new(1, "B", new PdfRect(10, 20, 20, 30)),
            new(2, "C", new PdfRect(0, 0, 10, 10)),
            new(3, "D", new PdfRect(10, 0, 20, 10)),
        };

        PdfTextSelection.CopyCharsInRect(chars, new PdfRect(0, 0, 25, 35)).Should().Be("AB\nCD");
    }

    [Fact]
    public void CopyAcrossLines_selects_stream_range_with_reading_order()
    {
        var chars = new List<PdfTextChar>
        {
            new(0, "H", new PdfRect(0, 20, 8, 30)),
            new(1, "i", new PdfRect(8, 20, 14, 30)),
            new(2, "t", new PdfRect(0, 0, 8, 10)),
            new(3, "o", new PdfRect(8, 0, 16, 10)),
        };

        var text = PdfTextSelection.CopyAcrossLines(chars, startX: 4, startY: 25, endX: 12, endY: 5);
        text.Should().Be("Hi\nto");
    }

    [Fact]
    public void JoinInReadingOrder_inserts_space_for_word_gaps()
    {
        var chars = new List<PdfTextChar>
        {
            new(0, "Hello", new PdfRect(0, 0, 40, 10)),
            new(1, "world", new PdfRect(55, 0, 100, 10)),
        };

        PdfTextSelection.JoinInReadingOrder(chars).Should().Be("Hello world");
    }

    [Fact]
    public void CopyAll_selects_entire_page_in_reading_order()
    {
        var chars = new List<PdfTextChar>
        {
            new(0, "Top", new PdfRect(0, 20, 30, 30)),
            new(1, "Bot", new PdfRect(0, 0, 30, 10)),
        };

        PdfTextSelection.CopyAll(chars).Should().Be("Top\nBot");
        PdfTextSelection.CopyAll([]).Should().BeEmpty();
    }
}
