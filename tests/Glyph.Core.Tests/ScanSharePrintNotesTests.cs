using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.Printing;

namespace Glyph.Core.Tests;

public class ScanDialogToneAndPagesTests
{
    [Fact]
    public void Clamp_max_pages_and_tone()
    {
        ScanDialogUi.ClampMaxPages(0).Should().Be(1);
        ScanDialogUi.ClampMaxPages(99).Should().Be(50);
        ScanDialogUi.ClampMaxPages(12).Should().Be(12);
        ScanDialogUi.ClampTone(2000).Should().Be(1000);
        ScanDialogUi.ToneOrNull(0).Should().BeNull();
        ScanDialogUi.ToneOrNull(100).Should().Be(100);
        ScanDialogUi.BrightnessHeader.Should().Contain("Brightness");
        ScanDialogUi.MaxPagesHeader.Should().Contain("Max pages");
    }
}

public class PrintNotesUiTests
{
    [Fact]
    public void Include_checkbox_label()
    {
        PrintNotesUi.IncludeCheckbox.Should().Contain("notes");
    }
}

public class DocumentShareStatusTests
{
    [Fact]
    public void Status_and_mailto_copy()
    {
        DocumentShareStatus.MailtoSubject(null).Should().Be(DocumentShareStatus.DefaultSubject);
        DocumentShareStatus.MailtoSubject("Report.pdf").Should().Be("Report.pdf");
        DocumentShareStatus.ShareFailed("x").Should().Contain("x");
        DocumentShareStatus.MailtoBody.Should().Contain("Glyph");
    }
}
