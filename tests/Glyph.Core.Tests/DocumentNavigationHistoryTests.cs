using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class DocumentNavigationHistoryTests
{
    [Fact]
    public void Back_and_forward_traverse_history()
    {
        var history = new DocumentNavigationHistory();
        history.NavigateTo(0);
        history.NavigateTo(5);
        history.NavigateTo(2);

        history.GoBack().Should().Be(5);
        history.GoBack().Should().Be(0);
        history.CanGoBack.Should().BeFalse();
        history.GoForward().Should().Be(5);
        history.GoForward().Should().Be(2);
        history.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void New_navigation_clears_forward_stack()
    {
        var history = new DocumentNavigationHistory();
        history.NavigateTo(0);
        history.NavigateTo(4);
        history.GoBack();
        history.NavigateTo(7);
        history.CanGoForward.Should().BeFalse();
        history.Current.Should().Be(7);
    }
}
