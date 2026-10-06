using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfViewerChromeLabelsTests
{
    [Fact]
    public void Labels_are_stable()
    {
        PdfViewerChromeLabels.Aa.Should().NotBeNullOrEmpty();
        PdfViewerChromeLabels.Find.Should().NotBeNullOrEmpty();
        PdfViewerChromeLabels.FindSelection.Should().NotBeNullOrEmpty();
        PdfViewerChromeLabels.SortPageOrder.Should().Be("Page order");
        PdfViewerChromeLabels.SortRelevance.Should().Be("Relevance");
        PdfViewerChromeLabels.Aa.Should().Be("Aa");
    }
}
