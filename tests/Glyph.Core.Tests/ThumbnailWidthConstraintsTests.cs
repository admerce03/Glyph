using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class ThumbnailWidthConstraintsTests
{
    [Theory]
    [InlineData(50, 72)]
    [InlineData(72, 72)]
    [InlineData(108, 108)]
    [InlineData(180, 180)]
    [InlineData(300, 180)]
    public void Clamp(double input, double expected)
    {
        ThumbnailWidthConstraints.Clamp(input).Should().Be(expected);
    }
}
