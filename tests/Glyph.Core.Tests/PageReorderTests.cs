using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PageReorderTests
{
    [Fact]
    public void MoveSelection_moves_block_before_target()
    {
        // pages 0 1 2 3 4; move 1,2 before 4 → 0 3 1 2 4? 
        // insertBefore=4, remove 1,2 → remaining 0,3,4; removedBefore=2; target=2 → 0,3 + 1,2 + 4
        PageReorder.MoveSelection(5, [1, 2], insertBeforeIndex: 4)
            .Should().Equal(0, 3, 1, 2, 4);
    }

    [Fact]
    public void MoveSelection_to_start()
    {
        PageReorder.MoveSelection(4, [2, 3], insertBeforeIndex: 0)
            .Should().Equal(2, 3, 0, 1);
    }

    [Fact]
    public void MoveSelection_noop_when_empty()
    {
        PageReorder.MoveSelection(3, [], insertBeforeIndex: 1)
            .Should().Equal(0, 1, 2);
    }
}
