using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfViewerTooltipsTests
{
    [Fact]
    public void Core_tooltips_are_stable()
    {
        PdfViewerTooltips.MatchCase.Should().Be("Match case");
        PdfViewerTooltips.LongRunningJobProgress.Should().Contain("progress");
        PdfViewerTooltips.PresentationModeFullscreenHideChromeSingle.Should().Contain("Presentation");
        PdfDialogBodies.FlattenAnnotationsConfirm.Should().Contain("cannot be undone");
        PdfDialogBodies.WebcamUnavailableImport.Should().Contain("camera");
    }
}
