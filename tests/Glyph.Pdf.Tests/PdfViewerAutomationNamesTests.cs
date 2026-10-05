using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfViewerAutomationNamesTests
{
    [Fact]
    public void Labels_are_stable()
    {
        PdfViewerAutomationNames.TableOfContents.Should().Be("Table of contents");
    }
}
