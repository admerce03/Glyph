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
    }
}
