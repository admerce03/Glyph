using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfDialogPlaceholdersTests
{
    [Fact]
    public void Placeholders_are_stable()
    {
        PdfDialogPlaceholders.FindInDocument.Should().Contain("Find");
        PdfDialogPlaceholders.SignatureOfExample.Should().Contain("Signature");
        PdfDialogPlaceholders.NoteText.Should().Be("Note text");
    }
}
