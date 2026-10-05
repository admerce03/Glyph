using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageColorManagementDeferredTests
{
    [Fact]
    public void Documents_deferred_monitor_and_gamut()
    {
        ImageColorManagementDeferred.MonitorProfileSupported.Should().BeFalse();
        ImageColorManagementDeferred.GamutWarningOverlaySupported.Should().BeFalse();
        ImageColorManagementDeferred.MonitorProfileReason.Should().Contain("sRGB");
        ImageColorManagementDeferred.GamutWarningReason.Should().Contain("soft-proof");
    }
}
