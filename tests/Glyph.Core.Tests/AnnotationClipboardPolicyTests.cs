using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class AnnotationClipboardPolicyTests
{
    [Fact]
    public void Status_strings_and_cut_index_adjust()
    {
        AnnotationClipboardPolicy.Copied("Note").Should().Be("Copied Note.");
        AnnotationClipboardPolicy.Cut("Highlight").Should().Contain("removed on paste");
        AnnotationClipboardPolicy.Pasted("Ink", wasCut: true).Should().Contain("(cut)");
        AnnotationClipboardPolicy.Pasted("Ink", wasCut: false).Should().Be("Pasted Ink.");
        AnnotationClipboardPolicy.EmptyClipboard.Should().Contain("empty");

        AnnotationClipboardPolicy.AdjustIndexAfterCutRemove(0, 1, 0, 3).Should().Be(2);
        AnnotationClipboardPolicy.AdjustIndexAfterCutRemove(0, 2, 0, 1).Should().Be(1);
        AnnotationClipboardPolicy.AdjustIndexAfterCutRemove(0, 1, 1, 3).Should().Be(3);
        AnnotationClipboardPolicy.ClearClipboardAfterPaste(true).Should().BeTrue();
        AnnotationClipboardPolicy.ClearClipboardAfterPaste(false).Should().BeFalse();
    }
}
