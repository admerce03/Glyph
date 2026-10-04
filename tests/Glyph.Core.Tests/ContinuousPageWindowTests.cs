using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class ContinuousPageWindowTests
{
    [Fact]
    public void Around_clamps_to_document_bounds()
    {
        ContinuousPageWindow.Around(0, 100, radius: 4).Should().Be((0, 4));
        ContinuousPageWindow.Around(50, 100, radius: 4).Should().Be((46, 54));
        ContinuousPageWindow.Around(99, 100, radius: 4).Should().Be((95, 99));
    }

    [Fact]
    public void Around_handles_empty_document()
    {
        ContinuousPageWindow.Around(0, 0).Should().Be((0, -1));
    }
}
