using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfDialogTitlesTests
{
    [Fact]
    public void Titles_are_stable()
    {
        PdfDialogTitles.Ocr.Should().Be("OCR");
        PdfDialogTitles.PrintPdf.Should().Contain("Print");
        PdfDialogTitles.FlattenAnnotations.Should().Contain("Flatten");
        PdfDialogTitles.Signatures.Should().Be("Signatures");
    }
}
