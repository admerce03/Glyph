using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfRedactionUiCopyTests
{
    [Fact]
    public void ApplyDialogTitle_warns_permanent()
    {
        PdfRedactionUiCopy.ApplyDialogTitle.Should().Be("Apply redactions permanently?");
        PdfRedactionUiCopy.ApplyDialogTitle.Should().Contain("permanently");
    }

    [Fact]
    public void FormatApplyBody_mentions_cannot_be_undone()
    {
        var body = PdfRedactionUiCopy.FormatApplyBody(3);
        body.Should().Contain("3 redaction mark");
        body.Should().Contain("cannot be undone");
    }

    [Fact]
    public void FormatPendingStatus_empty_and_nonzero()
    {
        PdfRedactionUiCopy.FormatPendingStatus(0).Should().Be("No pending redactions.");
        PdfRedactionUiCopy.FormatPendingStatus(2).Should().Contain("2 pending mark");
        PdfRedactionUiCopy.FormatPendingStatus(2).Should().Contain("cannot be undone");
    }
}
