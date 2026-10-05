using FluentAssertions;
using Glyph.Core.Signatures;

namespace Glyph.Core.Tests;

public class SignatureStrokeRasterizerTests
{
    [Fact]
    public void Rasterize_produces_opaque_ink_and_transparent_background()
    {
        var points = new (double X, double Y)[]
        {
            (10, 10),
            (40, 30),
            (70, 15),
        };

        var raster = SignatureStrokeRasterizer.Rasterize(points, strokeWidthPoints: 2, paddingPoints: 4, pixelsPerPoint: 2);
        raster.PixelWidth.Should().BeGreaterThan(10);
        raster.PixelHeight.Should().BeGreaterThan(10);
        raster.BgraPixels.Length.Should().Be(raster.PixelWidth * raster.PixelHeight * 4);

        var opaque = 0;
        var transparent = 0;
        for (var i = 0; i < raster.BgraPixels.Length; i += 4)
        {
            var a = raster.BgraPixels[i + 3];
            if (a == 255)
            {
                opaque++;
            }
            else if (a == 0)
            {
                transparent++;
            }
        }

        opaque.Should().BeGreaterThan(20);
        transparent.Should().BeGreaterThan(opaque);
    }

    [Fact]
    public void Png_encoder_writes_valid_signature_header()
    {
        var points = new (double X, double Y)[] { (0, 0), (20, 10) };
        var raster = SignatureStrokeRasterizer.Rasterize(points);
        var png = SignaturePngEncoder.EncodeBgra(raster.BgraPixels, raster.PixelWidth, raster.PixelHeight);
        png.Should().HaveCountGreaterThan(50);
        png[0].Should().Be(0x89);
        png[1].Should().Be(0x50); // P
        png[2].Should().Be(0x4E); // N
        png[3].Should().Be(0x47); // G
    }
}
