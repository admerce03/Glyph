using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class ReadOnlySavePolicyTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    public void RequiresSaveAs(bool sessionRo, bool pathRo, bool expected)
    {
        ReadOnlySavePolicy.RequiresSaveAs(sessionRo, pathRo).Should().Be(expected);
    }

    [Fact]
    public void Dialog_copy_mentions_save_as()
    {
        ReadOnlySavePolicy.DialogMessage.Should().Contain("Save As");
        ReadOnlySavePolicy.CancelledStatus.Should().Contain("read-only");
    }
}
