using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class AnnotationSelectionStatusTests
{
    [Fact]
    public void Context_and_edit_prompts_are_stable()
    {
        AnnotationSelectionStatus.AnnotationContextHint.Should().Contain("right-click");
        AnnotationSelectionStatus.BookmarkContextHint.Should().Contain("bookmark");
        AnnotationSelectionStatus.AttachmentContextHint.Should().Contain("attachment");
        AnnotationSelectionStatus.SelectTextFirst.Should().Contain("text");
        AnnotationSelectionStatus.GroupAtLeastTwo.Should().Contain("two");
        AnnotationSelectionStatus.InkOrShapeWidth.Should().Contain("width");
        AnnotationSelectionStatus.Opacity.Should().Contain("opacity");
        AnnotationSelectionStatus.SaveAttachment.Should().Contain("attachment");
    }
}
