using FluentAssertions;
using Glyph.Core.Ocr;

namespace Glyph.Core.Tests;

public class OcrWordSelectionPolicyTests
{
    [Fact]
    public void Selection_and_copy_status()
    {
        OcrWordSelectionPolicy.ShouldClearBeforeToggle(ctrlHeld: false).Should().BeTrue();
        OcrWordSelectionPolicy.ShouldClearBeforeToggle(ctrlHeld: true).Should().BeFalse();
        OcrWordSelectionPolicy.SelectedWord("hello").Should().Contain("hello");
        OcrWordSelectionPolicy.CopiedWords(0).Should().Contain("All OCR");
        OcrWordSelectionPolicy.CopiedWords(3).Should().Contain("3");
        OcrWordSelectionPolicy.Cleared.Should().Contain("cleared");
    }
}
