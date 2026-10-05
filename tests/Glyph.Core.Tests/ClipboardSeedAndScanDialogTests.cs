using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class ClipboardTextSeedPolicyTests
{
    [Fact]
    public void Seeds_only_when_empty()
    {
        ClipboardTextSeedPolicy.ShouldSeed(null).Should().BeTrue();
        ClipboardTextSeedPolicy.ShouldSeed("x").Should().BeFalse();
        ClipboardTextSeedPolicy.ApplyClipboardText("", "hello").Should().Be("hello");
        ClipboardTextSeedPolicy.ApplyClipboardText("keep", "hello").Should().Be("keep");
    }
}

public class ScanDialogUiTests
{
    [Fact]
    public void Labels_and_dpi_clamp()
    {
        ScanDialogUi.SourceLabels.Should().Contain("Flatbed");
        ScanDialogUi.ColorModeLabels.Should().Contain("Grayscale");
        ScanDialogUi.ScannerHeader.Should().Be("Scanner");
        ScanDialogUi.DpiHeader.Should().Be("DPI");
        ScanDialogUi.DestinationHeader.Should().Contain("Destination");
        ScanDialogUi.ClampDpi(72).Should().Be(150);
        ScanDialogUi.ClampDpi(1200).Should().Be(600);
        ScanDialogUi.ClampDpi(300).Should().Be(300);
    }
}
