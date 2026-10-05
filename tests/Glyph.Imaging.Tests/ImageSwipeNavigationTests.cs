using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageSwipeNavigationTests
{
    [Fact]
    public void Resolves_horizontal_swipe_direction()
    {
        ImageSwipeNavigation.Resolve(0).Should().Be(ImageSwipeNavigation.Direction.None);
        ImageSwipeNavigation.Resolve(40, 0).Should().Be(ImageSwipeNavigation.Direction.None);
        ImageSwipeNavigation.Resolve(-100, 10).Should().Be(ImageSwipeNavigation.Direction.Next);
        ImageSwipeNavigation.Resolve(100, 10).Should().Be(ImageSwipeNavigation.Direction.Previous);
        ImageSwipeNavigation.Resolve(-100, 200).Should().Be(ImageSwipeNavigation.Direction.None);
        ImageSwipeNavigation.SiblingStep(ImageSwipeNavigation.Direction.Next).Should().Be(1);
        ImageSwipeNavigation.SiblingStep(ImageSwipeNavigation.Direction.Previous).Should().Be(-1);
    }
}
