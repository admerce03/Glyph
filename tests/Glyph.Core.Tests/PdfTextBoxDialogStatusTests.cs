using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PdfTextBoxDialogStatusTests
{
    [Fact]
    public void Status_strings()
    {
        PdfTextBoxDialogStatus.Title.Should().Be("Text box");
        PdfTextBoxDialogStatus.Added.Should().Contain("added");
        PdfTextBoxDialogStatus.Failed("x").Should().Contain("failed");
    }
}
