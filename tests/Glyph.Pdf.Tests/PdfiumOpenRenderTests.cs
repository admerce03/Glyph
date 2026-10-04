using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfiumOpenRenderTests
{
    [Fact]
    public async Task Open_and_render_sample_pdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-sample-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            CreateSamplePdf(path);

            var factory = new PdfiumDocumentFactory();
            var renderer = new PdfiumRenderer();

            await using var document = await factory.OpenAsync(path);
            document.PageCount.Should().Be(2);
            document.GetPage(0).WidthPoints.Should().BeGreaterThan(0);

            using var result = await renderer.RenderPageAsync(
                document,
                pageIndex: 0,
                new PdfRenderRequest(Scale: 1.0));

            result.Width.Should().BeGreaterThan(10);
            result.Height.Should().BeGreaterThan(10);
            result.Pixels.Length.Should().Be(result.Width * result.Height * 4);
            result.Pixels.ToArray().Any(b => b != 0 && b != 255).Should().BeTrue("rendered page should contain non-flat content");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void CreateSamplePdf(string path)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        var page1 = builder.AddPage(PageSize.A4);
        page1.AddText("Glyph sample page 1", 18, new PdfPoint(50, 750), font);

        var page2 = builder.AddPage(PageSize.A4);
        page2.AddText("Glyph sample page 2", 18, new PdfPoint(50, 750), font);

        File.WriteAllBytes(path, builder.Build());
    }
}
