using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfOutlinePatcherTests
{
    [Fact]
    public async Task Export_flat_bookmarks_readable_via_outline_service()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-outline-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var builder = new PdfDocumentBuilder();
            builder.AddPage(PageSize.Letter);
            builder.AddPage(PageSize.Letter);
            builder.AddPage(PageSize.Letter);
            File.WriteAllBytes(path, builder.Build());

            var factory = new PdfiumDocumentFactory();
            var export = new PdfiumOutlineExportService();
            var outlines = new PdfiumOutlineService();

            await using var document = await factory.OpenAsync(path);
            await export.ExportAsync(
                document,
                [
                    new PdfOutlineExportEntry("Cover", 0),
                    new PdfOutlineExportEntry("Chapter 2", 1),
                    new PdfOutlineExportEntry("Appendix", 2),
                ]);

            var nodes = await outlines.GetOutlineAsync(document);
            nodes.Should().HaveCount(3);
            nodes[0].Title.Should().Be("Cover");
            nodes[0].DestinationPageIndex.Should().Be(0);
            nodes[1].Title.Should().Be("Chapter 2");
            nodes[1].DestinationPageIndex.Should().Be(1);
            nodes[2].Title.Should().Be("Appendix");
            nodes[2].DestinationPageIndex.Should().Be(2);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
