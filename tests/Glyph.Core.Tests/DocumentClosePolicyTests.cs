using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class DocumentClosePolicyTests
{
    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, false, false)]
    public void RequiresDirtyPrompt(bool dirty, bool unsaved, bool skip, bool expected)
    {
        DocumentClosePolicy.RequiresDirtyPrompt(dirty, unsaved, skip).Should().Be(expected);
    }

    [Fact]
    public void UnsavedClosePrompt_includes_name()
    {
        DocumentClosePolicy.UnsavedClosePrompt("a.pdf").Should().Contain("a.pdf");
    }
}
