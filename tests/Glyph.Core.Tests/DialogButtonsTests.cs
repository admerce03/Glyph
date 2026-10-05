using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class DialogButtonsTests
{
    [Fact]
    public void Common_labels_are_stable()
    {
        DialogButtons.Cancel.Should().Be("Cancel");
        DialogButtons.Close.Should().Be("Close");
        DialogButtons.Apply.Should().Be("Apply");
        DialogButtons.Save.Should().Be("Save");
        DialogButtons.ExportEllipsis.Should().Contain("Export");
        DialogButtons.DrawNew.Should().Be("Draw new");
        DialogButtons.SaveAndDuplicate.Should().Contain("duplicate");
        DialogButtons.NotNow.Should().Be("Not now");
    }
}
