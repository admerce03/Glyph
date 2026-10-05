using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfAnnotationContentsCodecTests
{
    [Theory]
    [InlineData(0, "CalloutPointer:0")]
    [InlineData(12, "CalloutPointer:12")]
    public void Callout_pointer_round_trips(int owner, string expected)
    {
        var encoded = PdfAnnotationContentsCodec.FormatCalloutPointerContents(owner);
        encoded.Should().Be(expected);
        PdfAnnotationContentsCodec.IsCalloutPointerContents(encoded).Should().BeTrue();
        PdfAnnotationContentsCodec.TryParseCalloutPointerOwner(encoded, out var parsed).Should().BeTrue();
        parsed.Should().Be(owner);
    }

    [Fact]
    public void Callout_pointer_accepts_legacy_marker()
    {
        PdfAnnotationContentsCodec.IsCalloutPointerContents("CalloutPointer").Should().BeTrue();
        PdfAnnotationContentsCodec.TryParseCalloutPointerOwner("CalloutPointer", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(3, "GlyphTextUnderline:3")]
    [InlineData(0, "GlyphTextUnderline:0")]
    public void Text_underline_round_trips(int owner, string expected)
    {
        var encoded = PdfAnnotationContentsCodec.FormatTextUnderlineContents(owner);
        encoded.Should().Be(expected);
        PdfAnnotationContentsCodec.IsTextUnderlineContents(encoded).Should().BeTrue();
        PdfAnnotationContentsCodec.TryParseTextUnderlineOwner(encoded, out var parsed).Should().BeTrue();
        parsed.Should().Be(owner);
    }

    [Fact]
    public void Text_underline_accepts_legacy_marker()
    {
        PdfAnnotationContentsCodec.IsTextUnderlineContents("GlyphTextUnderline").Should().BeTrue();
        PdfAnnotationContentsCodec.TryParseTextUnderlineOwner("GlyphTextUnderline", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(PdfInkLineStyle.Solid, "Line")]
    [InlineData(PdfInkLineStyle.Dashed, "Line|Dashed")]
    public void Format_line_contents(PdfInkLineStyle style, string expected)
    {
        PdfAnnotationContentsCodec.FormatLineContents(style).Should().Be(expected);
    }

    [Theory]
    [InlineData(PdfArrowheadStyle.Open, PdfInkLineStyle.Solid, "Arrow")]
    [InlineData(PdfArrowheadStyle.Filled, PdfInkLineStyle.Solid, "Arrow|Filled")]
    [InlineData(PdfArrowheadStyle.Open, PdfInkLineStyle.Dashed, "Arrow|Dashed")]
    [InlineData(PdfArrowheadStyle.Filled, PdfInkLineStyle.Dashed, "Arrow|Filled|Dashed")]
    public void Format_arrow_contents(PdfArrowheadStyle head, PdfInkLineStyle line, string expected)
    {
        PdfAnnotationContentsCodec.FormatArrowContents(head, line).Should().Be(expected);
    }

    [Theory]
    [InlineData("Line", PdfShapeKind.Line)]
    [InlineData("Line|Dashed", PdfShapeKind.Line)]
    [InlineData("Arrow", PdfShapeKind.Arrow)]
    [InlineData("Arrow|Closed", PdfShapeKind.Arrow)]
    [InlineData("Freeform", PdfShapeKind.Freeform)]
    [InlineData("Star", PdfShapeKind.Star)]
    [InlineData("Polygon", PdfShapeKind.Polygon)]
    [InlineData("SpeechBubble", PdfShapeKind.SpeechBubble)]
    [InlineData("unknown", null)]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void From_ink_shape_contents(string? contents, PdfShapeKind? expected)
    {
        PdfAnnotationContentsCodec.FromInkShapeContents(contents).Should().Be(expected);
    }

    [Fact]
    public void Line_endpoints_round_trip()
    {
        var start = new PdfPagePoint(10.5, 20.25);
        var end = new PdfPagePoint(100, 200.125);
        var raw = PdfAnnotationContentsCodec.FormatLineEndpoints(start, end);
        PdfAnnotationContentsCodec.TryParseLineEndpoints(raw, out var a, out var b).Should().BeTrue();
        a.Should().Be(start);
        b.Should().Be(end);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1,2,3")]
    [InlineData("a,b,c,d")]
    [InlineData("GlyphLineEnds")]
    public void Line_endpoints_reject_invalid(string? raw)
    {
        PdfAnnotationContentsCodec.TryParseLineEndpoints(raw, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Parse_arrow_style_parts()
    {
        PdfAnnotationContentsCodec.ParseLineOrArrowStyle(
            "Arrow|Filled|Dashed",
            out var kind,
            out var line,
            out var head);
        kind.Should().Be(PdfShapeKind.Arrow);
        line.Should().Be(PdfInkLineStyle.Dashed);
        head.Should().Be(PdfArrowheadStyle.Filled);
    }

    [Fact]
    public void Parse_line_style_parts()
    {
        PdfAnnotationContentsCodec.ParseLineOrArrowStyle(
            "Line|Dashed",
            out var kind,
            out var line,
            out var head);
        kind.Should().Be(PdfShapeKind.Line);
        line.Should().Be(PdfInkLineStyle.Dashed);
        head.Should().Be(PdfArrowheadStyle.Open);
    }

    [Fact]
    public void Parse_empty_defaults_to_solid_open_line()
    {
        PdfAnnotationContentsCodec.ParseLineOrArrowStyle(
            null,
            out var kind,
            out var line,
            out var head);
        kind.Should().Be(PdfShapeKind.Line);
        line.Should().Be(PdfInkLineStyle.Solid);
        head.Should().Be(PdfArrowheadStyle.Open);
    }
}
