using FluentAssertions;
using Glyph.Core.Pdf;

namespace Glyph.Core.Tests;

public class PdfDocumentInfoUiTests
{
    [Fact]
    public void Dialog_labels()
    {
        PdfDocumentInfoUi.DialogTitle.Should().Contain("info");
        PdfDocumentInfoUi.EditDialogTitle.Should().Contain("Edit");
        PdfDocumentInfoUi.EditButton.Should().Contain("Edit");
        PdfDocumentInfoUi.EditableFieldLabels.Should().Contain(PdfDocumentInfoUi.FieldAuthor);
        PdfDocumentInfoUi.PropertiesUnavailablePrefix.Should().Contain("unavailable");
        PdfDocumentInfoUi.EditCancelledStatus.Should().Contain("cancelled");
        PdfDocumentInfoUi.ClearedStatus.Should().Contain("cleared");
        PdfDocumentInfoUi.UpdatedStatus.Should().Contain("updated");
        PdfDocumentInfoUi.FailedStatus("x").Should().Contain("x");
        PdfDocumentInfoUi.FormatSidebarSummary(
                "T", "A", "S", "C", "P", 3, "Letter", "a.pdf", "1 KB", "1.7", "", 2)
            .Should().Contain("Attachments: 2");
    }
}
