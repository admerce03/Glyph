using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfArrowGeometryTests
{
    [Fact]
    public void Open_head_produces_two_wing_strokes()
    {
        var start = new PdfPagePoint(0, 0);
        var end = new PdfPagePoint(100, 0);
        var (_, head) = PdfArrowGeometry.Build(start, end, PdfArrowheadStyle.Open, headLength: 20);
        head.Should().HaveCount(2);
        head[0][0].Should().Be(end);
    }

    [Fact]
    public void Filled_head_is_closed_triangle_stroke()
    {
        var start = new PdfPagePoint(0, 0);
        var end = new PdfPagePoint(100, 0);
        var (_, head) = PdfArrowGeometry.Build(start, end, PdfArrowheadStyle.Filled, headLength: 20);
        head.Should().ContainSingle();
        head[0].Should().HaveCount(4);
        head[0][0].Should().Be(head[0][^1]);
    }

    [Fact]
    public void Diamond_head_is_single_closed_stroke()
    {
        var start = new PdfPagePoint(0, 50);
        var end = new PdfPagePoint(80, 50);
        var (_, head) = PdfArrowGeometry.Build(start, end, PdfArrowheadStyle.Diamond, headLength: 16);
        head.Should().ContainSingle();
        head[0].Should().HaveCount(5);
    }
}
