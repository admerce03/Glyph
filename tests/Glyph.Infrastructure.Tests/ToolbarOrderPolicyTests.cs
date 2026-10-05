using FluentAssertions;
using Glyph.Infrastructure.Settings;

namespace Glyph.Infrastructure.Tests;

public class ToolbarOrderPolicyTests
{
    [Fact]
    public void Normalize_empty_is_catalog_order()
    {
        var order = ToolbarOrderPolicy.Normalize(null);
        order.Should().Equal(ToolbarCommands.Catalog.Select(c => c.Id));
        ToolbarOrderPolicy.IsDefault(null).Should().BeTrue();
        ToolbarOrderPolicy.IsDefault([]).Should().BeTrue();
    }

    [Fact]
    public void Normalize_puts_known_ids_first_and_drops_unknown()
    {
        var order = ToolbarOrderPolicy.Normalize(
        [
            ToolbarCommands.Ocr,
            "nope",
            ToolbarCommands.Share,
            ToolbarCommands.Ocr,
        ]);
        order[0].Should().Be(ToolbarCommands.Ocr);
        order[1].Should().Be(ToolbarCommands.Share);
        order.Should().Contain(ToolbarCommands.Sidebar);
        order.Should().HaveCount(ToolbarCommands.Catalog.Count);
        order.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Move_swaps_neighbors()
    {
        var moved = ToolbarOrderPolicy.Move([], ToolbarCommands.Previous, -1);
        moved[0].Should().Be(ToolbarCommands.Previous);
        moved[1].Should().Be(ToolbarCommands.Sidebar);

        var down = ToolbarOrderPolicy.Move(moved, ToolbarCommands.Previous, 1);
        down[0].Should().Be(ToolbarCommands.Sidebar);
        down[1].Should().Be(ToolbarCommands.Previous);
    }

    [Fact]
    public void ApplyOrderToItems_keeps_untagged_slots()
    {
        // Indices: 0 sidebar, 1 untagged, 2 prev, 3 next
        var items = new[] { "sidebar", "nav", "previous", "next" };
        var reordered = ToolbarOrderPolicy.ApplyOrderToItems(
            items,
            tag => tag == "nav" ? null : tag,
            [ToolbarCommands.Next, ToolbarCommands.Previous, ToolbarCommands.Sidebar]);

        reordered.Should().Equal("next", "nav", "previous", "sidebar");
    }

    [Fact]
    public void ApplyVisibilityAndOrder_hides_tagged_then_reorders()
    {
        var items = new[] { "previous", "status", "zoom", "next", "ocr" };
        string? Tag(string id) => id == "status" ? null : id;

        var result = ToolbarOrderPolicy.ApplyVisibilityAndOrder(
            items,
            Tag,
            [ToolbarCommands.Zoom],
            [ToolbarCommands.Ocr, ToolbarCommands.Previous, ToolbarCommands.Next]);

        result.Should().Equal("ocr", "status", "previous", "next");
    }

    [Fact]
    public void ApplyVisibilityAndOrder_empty_prefs_leave_sequence()
    {
        var items = new[] { "previous", "next", "zoom" };
        var result = ToolbarOrderPolicy.ApplyVisibilityAndOrder(
            items,
            id => id,
            [],
            []);

        result.Should().Equal(items);
    }

    [Fact]
    public void ApplyVisibilityAndOrder_never_hides_untagged()
    {
        var items = new[] { "previous", "status", "next" };
        var result = ToolbarOrderPolicy.ApplyVisibilityAndOrder(
            items,
            id => id == "status" ? null : id,
            [ToolbarCommands.Previous, "status"],
            null);

        result.Should().Equal("status", "next");
    }
}
