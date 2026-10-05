using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageSmartLassoEdgesTests
{
    [Fact]
    public void Sobel_highlights_vertical_edge()
    {
        var lum = new float[5, 5];
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                lum[y, x] = x < 2 ? 0f : 255f;
            }
        }

        var edges = ImageSmartLassoEdges.ComputeSobel(lum);
        edges[2, 2].Should().BeGreaterThan(edges[2, 0]);
        var snap = ImageSmartLassoEdges.FindStrongestEdge(edges, 1, 2, radius: 2);
        snap.X.Should().BeInRange(1, 3);
    }

    [Fact]
    public void Luminance_from_bgra_uses_standard_weights()
    {
        // B=0, G=0, R=255 → ~76.2
        Span<byte> bgra = stackalloc byte[] { 0, 0, 255, 255 };
        var lum = ImageSmartLassoEdges.LuminanceFromBgra(bgra, 1, 1);
        lum[0, 0].Should().BeApproximately(76.245f, 0.1f);
    }
}
