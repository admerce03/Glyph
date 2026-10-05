using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Text;

namespace Glyph.Pdf.Tests;

public class PdfTextSelectionWordExpandTests
{
    private static List<PdfTextChar> WordLine(params (string Value, double Left)[] glyphs)
    {
        var list = new List<PdfTextChar>(glyphs.Length);
        for (var i = 0; i < glyphs.Length; i++)
        {
            var (value, left) = glyphs[i];
            list.Add(new PdfTextChar(i, value, new PdfRect(left, 0, left + 8, 10)));
        }

        return list;
    }

    [Fact]
    public void TryExpandWordAt_expands_mid_word_hit_to_full_token()
    {
        var chars = WordLine(
            ("H", 0), ("e", 8), ("l", 16), ("l", 24), ("o", 32),
            (" ", 40),
            ("w", 48), ("o", 56), ("r", 64), ("l", 72), ("d", 80));

        PdfTextSelection.TryExpandWordAt(chars, x: 20, y: 0, out var start, out var end).Should().BeTrue();
        start.Should().Be(0);
        end.Should().Be(4);
        PdfTextSelection.CopyText(chars, start, end).Should().Be("Hello");
    }

    [Fact]
    public void TryExpandWordAt_selects_second_word_when_click_near_it()
    {
        var chars = WordLine(
            ("H", 0), ("i", 8),
            (" ", 16),
            ("t", 24), ("o", 32));

        PdfTextSelection.TryExpandWordAt(chars, x: 28, y: 0, out var start, out var end).Should().BeTrue();
        start.Should().Be(3);
        end.Should().Be(4);
        PdfTextSelection.CopyText(chars, start, end).Should().Be("to");
    }

    [Fact]
    public void TryExpandWordAt_on_whitespace_stays_on_space_glyph()
    {
        var chars = WordLine(("A", 0), (" ", 10), ("B", 20));
        PdfTextSelection.TryExpandWordAt(chars, x: 10, y: 0, out var start, out var end).Should().BeTrue();
        start.Should().Be(1);
        end.Should().Be(1);
    }

    [Fact]
    public void TryExpandWordAt_empty_returns_false()
    {
        PdfTextSelection.TryExpandWordAt([], 0, 0, out var start, out var end).Should().BeFalse();
        start.Should().Be(0);
        end.Should().Be(-1);
    }

    [Fact]
    public void TryExpandWordAt_single_char_page()
    {
        var chars = WordLine(("Z", 5));
        PdfTextSelection.TryExpandWordAt(chars, x: 100, y: 100, out var start, out var end).Should().BeTrue();
        start.Should().Be(0);
        end.Should().Be(0);
    }

    [Theory]
    [InlineData(true, 10, 10, true)]
    [InlineData(false, 100, 20, true)]  // wide short
    [InlineData(false, 30, 20, false)] // not wide enough
    [InlineData(false, 100, 10, false)] // height too small
    public void PreferColumnMode_alt_or_aspect(bool alt, double w, double h, bool expected)
    {
        PdfTextSelection.PreferColumnMode(alt, w, h).Should().Be(expected);
    }
}
