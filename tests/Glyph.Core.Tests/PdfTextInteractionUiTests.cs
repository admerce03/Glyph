using FluentAssertions;
using Glyph.Core.Text;

namespace Glyph.Core.Tests;

public class PdfTextInteractionUiTests
{
    [Fact]
    public void Labels_cover_context_menu()
    {
        PdfTextInteractionUi.Copy.Should().Be("Copy");
        PdfTextInteractionUi.SearchWeb.Should().Be("Search web");
        PdfTextInteractionUi.CopyRegionAsImage.Should().Contain("region");
        PdfTextInteractionUi.CopiedCharacters(12).Should().Be("Copied 12 characters.");
    }
}
