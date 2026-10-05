using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class AppShellTextLabelsTests
{
    [Fact]
    public void Labels_are_stable()
    {
        AppShellTextLabels.CloseTab.Should().Be("Close Tab");
    }
}
