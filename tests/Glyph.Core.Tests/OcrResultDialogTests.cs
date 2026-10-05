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

    [Fact]
    public void Progress_export_and_engine_status()
    {
        OcrResultDialog.Cancelling.Should().Contain("Cancelling");
        OcrResultDialog.EngineUnavailable.Should().Contain("unavailable");
        OcrResultDialog.NoPagesSelected.Should().Contain("No pages");
        OcrResultDialog.OverlaysCleared.Should().Contain("cleared");
        OcrResultDialog.SearchableExportNeedsOcr.Should().Contain("before exporting");
        OcrResultDialog.BuildingSearchablePdf.Should().Contain("Building");
        OcrResultDialog.SearchablePdfCancelled.Should().Contain("cancelled");
        OcrResultDialog.SearchablePdfFailed("x").Should().Contain("failed: x");
        OcrResultDialog.SavedSearchablePdf(2, "a.pdf").Should().Contain("a.pdf");
        OcrResultDialog.SavedSearchablePdf(2, "a.pdf").Should().Contain("2 page");
    }
}
