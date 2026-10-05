using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class AnnotationJumpStatusTests
{
    [Fact]
    public void Jumped_to_label()
    {
        AnnotationJumpStatus.JumpedTo("Highlight p2").Should().Be("Jumped to Highlight p2.");
    }
}
