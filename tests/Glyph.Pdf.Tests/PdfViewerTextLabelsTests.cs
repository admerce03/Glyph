using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfViewerTextLabelsTests
{
    [Fact]
    public void Labels_are_stable()
    {
        PdfViewerTextLabels.Bookmarks.Should().Be("Bookmarks");
    }
}
