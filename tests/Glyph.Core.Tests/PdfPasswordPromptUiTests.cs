using FluentAssertions;
using Glyph.Core.Pdf;

namespace Glyph.Core.Tests;

public class PdfPasswordPromptUiTests
{
    [Fact]
    public void Titles_and_status()
    {
        PdfPasswordPromptUi.DialogTitle(false).Should().Be(PdfPasswordPromptUi.Title);
        PdfPasswordPromptUi.DialogTitle(true).Should().Be(PdfPasswordPromptUi.RetryTitle);
        PdfPasswordPromptUi.PromptBody("a.pdf").Should().Contain("a.pdf");
        PdfPasswordPromptUi.IncorrectStatus.Should().Contain("Incorrect");
        PdfPasswordPromptUi.CancelledStatus.Should().Contain("cancelled");
        PdfPasswordPromptUi.MaxAttempts.Should().BeGreaterThan(0);
    }
}
