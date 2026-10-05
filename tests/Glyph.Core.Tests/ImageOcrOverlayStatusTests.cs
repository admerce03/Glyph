using FluentAssertions;
using Glyph.Core.Ocr;

namespace Glyph.Core.Tests;

public class ImageOcrOverlayStatusTests
{
    [Fact]
    public void Find_and_ready_status_strings()
    {
        ImageOcrOverlayStatus.RunOcrBeforeSearch.Should().Contain("before searching");
        ImageOcrOverlayStatus.RunOcrFirst.Should().Contain("OCR");
        ImageOcrOverlayStatus.NoMatches.Should().Contain("No OCR");
        ImageOcrOverlayStatus.FormatMatches(3).Should().Contain("3");
        ImageOcrOverlayStatus.FormatMatch(2, 5, "hello").Should().Contain("2/5");
        ImageOcrOverlayStatus.FormatMatch(2, 5, "hello").Should().Contain("hello");
        ImageOcrOverlayStatus.FormatReady(0).Should().Contain("no text");
        ImageOcrOverlayStatus.FormatReady(12).Should().Contain("click words");
        ImageOcrOverlayStatus.FormatReady(12).Should().Contain("12");
        ImageOcrOverlayStatus.FormatEntitiesHeader(4).Should().Contain("4");
        ImageOcrOverlayStatus.NoEntitiesDetected.Should().Contain("URLs");
        ImageOcrOverlayStatus.OpenedCalendarInvite.Should().Contain("calendar");
        ImageOcrOverlayStatus.FormatCopiedEntityUnparsed("Date").Should().Contain("Date");
    }
}
