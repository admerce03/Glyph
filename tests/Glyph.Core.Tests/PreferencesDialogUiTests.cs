using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class PreferencesDialogUiTests
{
    [Fact]
    public void Labels_and_combo_maps()
    {
        PreferencesDialogUi.DialogTitle.Should().Be("Preferences");
        PreferencesDialogUi.PdfLayoutLabels.Should().HaveCount(4);
        PreferencesDialogUi.InterpolationIndex("Bicubic").Should().Be(3);
        PreferencesDialogUi.InterpolationSetting(2).Should().Be("Bilinear");
        PreferencesDialogUi.Zoom100Index("Print").Should().Be(1);
        PreferencesDialogUi.Zoom100Setting(0).Should().Be("Pixels");
        PreferencesDialogUi.RestoreTabs.Should().Contain("startup");
        PreferencesDialogUi.LocalOcrNote.Should().Contain("Windows OCR");
        PreferencesDialogUi.ClearSavedSignatures.Should().Contain("signatures");
        PreferencesDialogUi.PrivacyHeader.Should().Be("Privacy");
    }
}
