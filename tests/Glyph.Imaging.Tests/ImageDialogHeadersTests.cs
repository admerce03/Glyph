using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Xunit;

namespace Glyph.Imaging.Tests;

public class ImageDialogHeadersTests
{
    [Fact]
    public void Headers_are_stable()
    {
        ImageDialogHeaders.DpiPpi.Should().Contain("DPI");
        ImageDialogHeaders.Scale.Should().Be("Scale");
        ImageDialogHeaders.QualityJpegWebpAvif.Should().Contain("Quality");
        ImageDialogHeaders.Brightness.Should().Contain("Brightness");
        ImageDialogHeaders.CalloutText.Should().Be("Callout text");
    }
}
