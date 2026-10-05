using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfSpeechBubbleGeometryTests
{
    [Fact]
    public void BuildPoints_returns_closed_bubble_with_pointer_at_bottom()
    {
        var points = PdfSpeechBubbleGeometry.BuildPoints(new PdfRect(10, 20, 110, 120));
        points.Should().HaveCountGreaterThan(6);
        points[0].Should().Be(points[^1]);

        // Tip is the lowest Y (PDF Y-up → bottom of bounds).
        var tip = points[0];
        tip.Y.Should().BeApproximately(20, 0.01);
        tip.X.Should().BeApproximately(10 + (100 * 0.22), 0.5);

        // Body should reach near the top of the bounds.
        points.Max(p => p.Y).Should().BeApproximately(120, 0.01);
    }
}
