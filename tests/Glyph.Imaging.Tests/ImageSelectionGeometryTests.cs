using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Xunit;

namespace Glyph.Imaging.Tests;

public class ImageSelectionGeometryTests
{
    [Fact]
    public void ContainsInPolygon_requires_three_vertices()
    {
        ImageSelectionGeometry.ContainsInPolygon(0, 0, []).Should().BeFalse();
        ImageSelectionGeometry.ContainsInPolygon(0, 0, [(0, 0), (1, 0)]).Should().BeFalse();
    }

    [Fact]
    public void ContainsInPolygon_triangle_inside_outside()
    {
        var tri = new (double X, double Y)[] { (0, 0), (10, 0), (0, 10) };
        ImageSelectionGeometry.ContainsInPolygon(2, 2, tri).Should().BeTrue();
        ImageSelectionGeometry.ContainsInPolygon(9, 9, tri).Should().BeFalse();
    }

    [Fact]
    public void ContainsInEllipse_center_and_outside()
    {
        ImageSelectionGeometry.ContainsInEllipse(5, 5, 0, 0, 10, 10).Should().BeTrue();
        ImageSelectionGeometry.ContainsInEllipse(0, 0, 0, 0, 10, 10).Should().BeFalse();
        ImageSelectionGeometry.ContainsInEllipse(5, 5, 0, 0, 0, 10).Should().BeFalse();
    }

    [Fact]
    public void ContainsInRect_bounds()
    {
        ImageSelectionGeometry.ContainsInRect(5, 5, 0, 0, 10, 10).Should().BeTrue();
        ImageSelectionGeometry.ContainsInRect(10, 10, 0, 0, 10, 10).Should().BeTrue();
        ImageSelectionGeometry.ContainsInRect(10.1, 5, 0, 0, 10, 10).Should().BeFalse();
    }
}
