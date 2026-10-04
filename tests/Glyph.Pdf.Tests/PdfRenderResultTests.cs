using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfRenderResultTests
{
    [Fact]
    public void Dispose_clears_pixel_access()
    {
        var pixels = new byte[4];
        var result = new PdfRenderResult(1, 1, pixels);

        result.Width.Should().Be(1);
        result.Pixels.Length.Should().Be(4);

        result.Dispose();
        var act = () => _ = result.Pixels.Length;
        act.Should().Throw<ObjectDisposedException>();
    }
}
