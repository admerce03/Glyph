using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfShapeFillPolicyTests
{
    [Fact]
    public void Fill_from_stroke_for_closed_shapes_only()
    {
        var stroke = new PdfAnnotationColor(100, 180, 30);
        PdfShapeFillPolicy.UsesFill(PdfShapeKind.Rectangle).Should().BeTrue();
        PdfShapeFillPolicy.UsesFill(PdfShapeKind.Line).Should().BeFalse();
        PdfShapeFillPolicy.UsesFill(PdfShapeKind.Loupe).Should().BeFalse();

        var fill = PdfShapeFillPolicy.FromStroke(PdfShapeKind.Ellipse, stroke);
        fill.Should().NotBeNull();
        fill!.Value.R.Should().Be(100);
        fill.Value.A.Should().Be(PdfShapeFillPolicy.SemiTransparentAlpha);
        PdfShapeFillPolicy.FromStroke(PdfShapeKind.Arrow, stroke).Should().BeNull();
    }
}
