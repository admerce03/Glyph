using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class SidebarModeComboTests
{
    [Fact]
    public void Labels_cover_seven_modes()
    {
        SidebarModeCombo.Labels.Should().HaveCount(7);
        SidebarModeCombo.Labels[SidebarModeCombo.SearchIndex].Should().Be("Search");
        SidebarModeCombo.Labels[SidebarModeCombo.AttachmentsIndex].Should().Be("Attachments");
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(6, 6)]
    [InlineData(99, 6)]
    public void ClampIndex(int input, int expected)
    {
        SidebarModeCombo.ClampIndex(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, SidebarMode.Thumbnails)]
    [InlineData(1, SidebarMode.TableOfContents)]
    [InlineData(3, SidebarMode.SearchResults)]
    [InlineData(6, SidebarMode.Attachments)]
    public void ToSidebarMode(int index, SidebarMode expected)
    {
        SidebarModeCombo.ToSidebarMode(index).Should().Be(expected);
    }

    [Theory]
    [InlineData(SidebarMode.SearchResults, 3)]
    [InlineData(SidebarMode.Annotations, 4)]
    [InlineData(SidebarMode.ContactSheet, 0)]
    public void FromSidebarMode(SidebarMode mode, int expected)
    {
        SidebarModeCombo.FromSidebarMode(mode).Should().Be(expected);
    }
}
