using FluentAssertions;
using Glyph.Core.Ocr;

namespace Glyph.Core.Tests;

public class OcrResultDialogTests
{
    [Fact]
    public void Titles_summary_and_completion_status()
    {
        OcrResultDialog.Title(1).Should().Be("OCR result");
        OcrResultDialog.Title(3).Should().Be("OCR results");
        OcrResultDialog.Summary(1, 2, 4, 20).Should().Contain("Page 3");
        OcrResultDialog.Summary(2, 0, 4, 20).Should().StartWith("2 pages");
        OcrResultDialog.CompletionStatus(1, 0, 0).Should().Contain("no text");
        OcrResultDialog.CompletionStatus(2, 0, 5).Should().Contain("Click words");
        OcrResultDialog.TextCopied.Should().Contain("copied");
        OcrResultDialog.PageSectionHeader(1, "hi").Should().Contain("Page 2");
    }
}
