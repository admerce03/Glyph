using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfOptimizeDialogUiTests
{
    [Fact]
    public void Preset_combo_maps()
    {
        PdfOptimizeDialogUi.PresetLabels.Should().HaveCount(5);
        PdfOptimizeDialogUi.FromComboIndex(0).Should().Be(PdfOptimizePreset.Lossless);
        PdfOptimizeDialogUi.FromComboIndex(2).Should().Be(PdfOptimizePreset.Balanced);
        PdfOptimizeDialogUi.FromComboIndex(4).Should().Be(PdfOptimizePreset.Custom);
        PdfOptimizeDialogUi.IsCustomIndex(4).Should().BeTrue();
        PdfOptimizeDialogUi.IsCustomIndex(2).Should().BeFalse();
    }

    [Fact]
    public void Result_status_includes_counts()
    {
        var status = PdfOptimizeDialogUi.ResultStatus(
            new PdfOptimizeResult(3, 1, 1000, 800),
            "1 KB",
            "800 B");
        status.Should().Contain("3");
        status.Should().Contain("1");
        status.Should().Contain("1 KB");
        status.Should().Contain("Save to keep");
    }
}
