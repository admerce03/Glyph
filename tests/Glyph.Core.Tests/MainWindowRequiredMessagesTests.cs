using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class MainWindowRequiredMessagesTests
{
    [Fact]
    public void Messages_are_stable()
    {
        MainWindowRequiredMessages.SavePicker.Should().Contain("save picker");
        MainWindowRequiredMessages.Print.Should().Contain("print");
        MainWindowRequiredMessages.For("export").Should().Be(MainWindowRequiredMessages.Export);
    }
}
