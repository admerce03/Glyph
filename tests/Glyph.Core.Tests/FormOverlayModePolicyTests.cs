using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class FormOverlayModePolicyTests
{
    [Fact]
    public void Focus_wrap_and_status()
    {
        FormOverlayModePolicy.NextFocusIndex(0, 3, forward: true).Should().Be(1);
        FormOverlayModePolicy.NextFocusIndex(2, 3, forward: true).Should().Be(0);
        FormOverlayModePolicy.NextFocusIndex(0, 3, forward: false).Should().Be(2);
        FormOverlayModePolicy.Started.Should().Contain("Tab");
        FormOverlayModePolicy.NoFields.Should().Contain("no widget");
        FormOverlayModePolicy.NoAcroFormFields.Should().Contain("No AcroForm");
        FormOverlayModePolicy.Flattening.Should().Contain("Flattening");
        FormOverlayModePolicy.NothingToFlatten.Should().Contain("Nothing");
        FormOverlayModePolicy.FormatSignedField("A", "B").Should().Contain("A");
        FormOverlayModePolicy.FormatSignedField("A", "B").Should().Contain("B");
    }
}
