using FluentAssertions;
using Glyph.Core.Pdf;

namespace Glyph.Core.Tests;

public class PdfDocumentInfoUiTests
{
    [Fact]
    public void Dialog_labels()
    {
        PdfDocumentInfoUi.DialogTitle.Should().Contain("info");
        PdfDocumentInfoUi.EditButton.Should().Contain("Edit");
        PdfDocumentInfoUi.PropertiesUnavailablePrefix.Should().Contain("unavailable");
    }
}
