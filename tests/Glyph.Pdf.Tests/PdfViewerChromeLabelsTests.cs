using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfViewerChromeLabelsTests
{
    [Fact]
    public void Labels_are_stable()
    {
        PdfViewerChromeLabels.Aa.Should().NotBeNullOrEmpty();
        PdfViewerChromeLabels.Find.Should().NotBeNullOrEmpty();
        PdfViewerChromeLabels.FindSelection.Should().NotBeNullOrEmpty();
        PdfViewerChromeLabels.Aa.Should().Be("Aa");
    }
}
