using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class NonDestructiveEditPolicyTests
{
    [Fact]
    public void In_memory_until_save_and_flatten_prompt()
    {
        NonDestructiveEditPolicy.ImageEditsStayInMemoryUntilSave.Should().BeTrue();
        NonDestructiveEditPolicy.CropBoxPreservesSourcePixels.Should().BeTrue();
        NonDestructiveEditPolicy.ShouldPromptFlatten(true, true).Should().BeTrue();
        NonDestructiveEditPolicy.ShouldPromptFlatten(true, false).Should().BeFalse();
        NonDestructiveEditPolicy.ShouldPromptFlatten(false, true).Should().BeFalse();
    }
}
