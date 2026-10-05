using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class PdfOperationFailedStatusTests
{
    [Fact]
    public void Format_prefixes_message()
    {
        PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Save, "disk").Should().Be("Save failed: disk");
        PdfOperationFailedStatus.FormatFreeformOrInk(true, "x").Should().StartWith("Freeform");
        PdfOperationFailedStatus.FormatFreeformOrInk(false, "x").Should().StartWith("Ink");
        PdfOperationFailedStatus.FormFill.Should().Contain("Form fill");
        PdfOperationFailedStatus.AttachmentSave.Should().Contain("Attachment");
    }
}
