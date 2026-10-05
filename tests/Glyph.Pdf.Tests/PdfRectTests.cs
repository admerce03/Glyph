using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfRectTests
{
    [Fact]
    public void Intersects_overlap_and_touching_edges()
    {
        var a = new PdfRect(0, 0, 10, 10);
        var overlap = new PdfRect(5, 5, 15, 15);
        var touchingRight = new PdfRect(10, 0, 20, 10);
        var separate = new PdfRect(11, 0, 20, 10);

        a.Intersects(overlap).Should().BeTrue();
        overlap.Intersects(a).Should().BeTrue();
        // Edge-touching is not an intersection (strict < / >).
        a.Intersects(touchingRight).Should().BeFalse();
        a.Intersects(separate).Should().BeFalse();
    }

    [Fact]
    public void Width_Height_and_ContainsPoint()
    {
        var rect = new PdfRect(10, 20, 40, 50);
        rect.Width.Should().Be(30);
        rect.Height.Should().Be(30);
        rect.ContainsPoint(10, 20).Should().BeTrue();
        rect.ContainsPoint(40, 50).Should().BeTrue();
        rect.ContainsPoint(9, 20).Should().BeFalse();
        rect.ContainsPoint(25, 51).Should().BeFalse();
    }

    [Fact]
    public void CoverageBy_reports_overlap_fraction_for_redaction_threshold()
    {
        var obj = new PdfRect(0, 0, 100, 100);
        obj.CoverageBy(new PdfRect(0, 0, 100, 100)).Should().BeApproximately(1.0, 1e-9);
        obj.CoverageBy(new PdfRect(0, 0, 50, 100)).Should().BeApproximately(0.5, 1e-9);
        obj.CoverageBy(new PdfRect(0, 0, 40, 100)).Should().BeApproximately(0.4, 1e-9);
        obj.CoverageBy(new PdfRect(0, 0, 30, 100)).Should().BeApproximately(0.3, 1e-9);
        // Redaction sanitize uses >= 0.35 coverage.
        (obj.CoverageBy(new PdfRect(0, 0, 35, 100)) >= 0.35).Should().BeTrue();
        (obj.CoverageBy(new PdfRect(0, 0, 34, 100)) >= 0.35).Should().BeFalse();
        obj.CoverageBy(new PdfRect(200, 200, 300, 300)).Should().Be(0);
        new PdfRect(0, 0, 0, 10).CoverageBy(obj).Should().Be(0);
    }
}
