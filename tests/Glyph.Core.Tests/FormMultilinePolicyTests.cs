using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class FormMultilinePolicyTests
{
    [Fact]
    public void Text_fields_accept_return()
    {
        FormMultilinePolicy.TextFieldAcceptsReturn.Should().BeTrue();
        FormMultilinePolicy.AcceptsReturnFor("TextField").Should().BeTrue();
        FormMultilinePolicy.AcceptsReturnFor("Checkbox").Should().BeFalse();
    }
}
