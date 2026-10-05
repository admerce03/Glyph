using FluentAssertions;
using Glyph.Infrastructure.Settings;

namespace Glyph.Infrastructure.Tests;

public class ToolbarCommandsTests
{
    [Fact]
    public void Catalog_covers_all_F54_command_ids_uniquely()
    {
        var ids = ToolbarCommands.Catalog.Select(c => c.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().BeEquivalentTo(
        [
            ToolbarCommands.Sidebar,
            ToolbarCommands.Previous,
            ToolbarCommands.Next,
            ToolbarCommands.PageNumber,
            ToolbarCommands.Zoom,
            ToolbarCommands.FitPage,
            ToolbarCommands.FitWidth,
            ToolbarCommands.Search,
            ToolbarCommands.Markup,
            ToolbarCommands.Highlight,
            ToolbarCommands.Rotate,
            ToolbarCommands.Crop,
            ToolbarCommands.Signature,
            ToolbarCommands.Print,
            ToolbarCommands.Inspector,
            ToolbarCommands.Share,
            ToolbarCommands.Ocr,
        ]);
        ToolbarCommands.Catalog.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Label));
    }
}
