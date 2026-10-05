using FluentAssertions;
using Glyph.Core.Signatures;

namespace Glyph.Core.Tests;

public sealed class SignaturePaperKeyingTests
{
    [Fact]
    public void KeyOutNearWhite_clears_white_paper_keeps_dark_ink()
    {
        // 2×1: white pixel, dark ink pixel
        var bgra = new byte[]
        {
            255, 255, 255, 255,
            20, 20, 20, 255,
        };

        SignaturePaperKeying.KeyOutNearWhite(bgra, width: 2, height: 1);

        bgra[3].Should().Be(0);
        bgra[7].Should().Be(255);
    }

    [Fact]
    public void KeyOutNearWhite_soft_edge_reduces_near_white_alpha()
    {
        var bgra = new byte[]
        {
            220, 220, 220, 255, // near white, inside soft band for default threshold 235 / softness 28
        };

        SignaturePaperKeying.KeyOutNearWhite(bgra, width: 1, height: 1);

        bgra[3].Should().BeLessThan(255);
        bgra[3].Should().BeGreaterThan(0);
    }

    [Fact]
    public void KeyOutNearWhite_rejects_short_buffer()
    {
        var act = () => SignaturePaperKeying.KeyOutNearWhite(new byte[4], width: 2, height: 1);
        act.Should().Throw<ArgumentException>();
    }
}
