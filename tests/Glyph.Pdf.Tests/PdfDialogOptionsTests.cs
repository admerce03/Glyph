using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfDialogOptionsTests
{
    [Fact]
    public void Option_lists_are_stable()
    {
        PdfDialogOptions.LayoutModes.Should().Contain("Continuous");
        PdfDialogOptions.StandardFonts.Should().Contain("Helvetica");
        PdfDialogOptions.HorizontalAlignments.Should().Equal("Left", "Center", "Right");
        PdfDialogOptions.NoteColors.Should().Contain("Yellow");
        PdfDialogOptions.NoneChoice.Should().Be("(none)");
    }
}
