using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfStarGeometryTests
{
    [Fact]
    public void BuildPoints_returns_closed_five_point_star()
    {
        var points = PdfStarGeometry.BuildPoints(new PdfRect(0, 0, 100, 100));
        points.Should().HaveCount(11);
        points[0].Should().Be(points[^1]);

        // Tip should be the topmost outer vertex (PDF Y up → largest Y).
        var tip = points[0];
        tip.Y.Should().BeApproximately(100, 0.01);
        tip.X.Should().BeApproximately(50, 0.01);

        // Outer tips alternate every other vertex among the first 10.
        for (var i = 0; i < 10; i += 2)
        {
            var dist = Math.Sqrt(Math.Pow(points[i].X - 50, 2) + Math.Pow(points[i].Y - 50, 2));
            dist.Should().BeApproximately(50, 0.01);
        }
    }
}
