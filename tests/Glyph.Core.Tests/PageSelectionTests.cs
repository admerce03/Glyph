using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PageSelectionTests
{
    [Fact]
    public void SelectOnly_replaces_previous_selection()
    {
        var selection = new PageSelection();
        selection.SelectOnly(1);
        selection.SelectOnly(3);

        selection.SelectedIndexes.Should().Equal(3);
        selection.Count.Should().Be(1);
    }

    [Fact]
    public void Toggle_adds_and_removes_indexes()
    {
        var selection = new PageSelection();
        selection.Toggle(0);
        selection.Toggle(2);
        selection.SelectedIndexes.Should().Equal(0, 2);

        selection.Toggle(0);
        selection.SelectedIndexes.Should().Equal(2);
    }

    [Fact]
    public void SelectRange_uses_anchor_from_last_non_range_click()
    {
        var selection = new PageSelection();
        selection.SelectOnly(1);
        selection.SelectRange(4);

        selection.SelectedIndexes.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void ApplyClick_shift_extends_range_and_ctrl_toggles()
    {
        var selection = new PageSelection();
        selection.ApplyClick(2, ctrlOrMeta: false, shift: false).Should().BeTrue();
        selection.ApplyClick(4, ctrlOrMeta: false, shift: true).Should().BeTrue();
        selection.SelectedIndexes.Should().Equal(2, 3, 4);

        selection.ApplyClick(3, ctrlOrMeta: true, shift: false).Should().BeTrue();
        selection.SelectedIndexes.Should().Equal(2, 4);
    }

    [Fact]
    public void SelectAll_selects_every_page()
    {
        var selection = new PageSelection();
        selection.SelectAll(4);
        selection.SelectedIndexes.Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public void ApplyKeyboardMove_extends_with_shift()
    {
        var selection = new PageSelection();
        selection.SelectOnly(1);
        selection.ApplyKeyboardMove(3, extendRange: true);
        selection.SelectedIndexes.Should().Equal(1, 2, 3);

        selection.ApplyKeyboardMove(0, extendRange: false);
        selection.SelectedIndexes.Should().Equal(0);
    }
}
