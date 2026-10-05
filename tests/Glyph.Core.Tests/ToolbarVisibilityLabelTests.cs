using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class ToolbarVisibilityLabelTests
{
    [Theory]
    [InlineData(true, "Hide Toolbar")]
    [InlineData(false, "Show Toolbar")]
    public void For_toggles_label(bool visible, string expected)
    {
        ToolbarVisibilityLabel.For(visible).Should().Be(expected);
    }
}
