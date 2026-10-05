using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfInkLineStyleGeometryTests
{
    [Fact]
    public void Solid_returns_single_segment()
    {
        var start = new PdfPagePoint(0, 0);
        var end = new PdfPagePoint(100, 0);
        var segments = PdfInkLineStyleGeometry.Segment(start, end, PdfInkLineStyle.Solid, 2f);
        segments.Should().ContainSingle();
        segments[0].Start.Should().Be(start);
        segments[0].End.Should().Be(end);
    }

    [Fact]
    public void Dashed_splits_long_horizontal_line()
    {
        var start = new PdfPagePoint(0, 0);
        var end = new PdfPagePoint(200, 0);
        var segments = PdfInkLineStyleGeometry.Segment(start, end, PdfInkLineStyle.Dashed, 2f);
        segments.Count.Should().BeGreaterThan(2);
        segments[0].Start.X.Should().BeApproximately(0, 0.01);
        segments[^1].End.X.Should().BeLessThan(200);
    }

    [Fact]
    public void Dotted_produces_more_segments_than_dashed_for_same_length()
    {
        var start = new PdfPagePoint(0, 0);
        var end = new PdfPagePoint(120, 0);
        var dashed = PdfInkLineStyleGeometry.Segment(start, end, PdfInkLineStyle.Dashed, 2f);
        var dotted = PdfInkLineStyleGeometry.Segment(start, end, PdfInkLineStyle.Dotted, 2f);
        dotted.Count.Should().BeGreaterThan(dashed.Count);
    }
}
