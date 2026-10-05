using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfDialogHeadersTests
{
    [Fact]
    public void Headers_are_stable()
    {
        PdfDialogHeaders.FontSizePt.Should().Contain("Font size");
        PdfDialogHeaders.AccessibilityDescription.Should().Contain("Accessibility");
        PdfDialogHeaders.PagesPerSheet.Should().Contain("Pages");
    }
}
