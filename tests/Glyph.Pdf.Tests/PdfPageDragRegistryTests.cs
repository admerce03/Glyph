using FluentAssertions;
using Glyph.Pdf.Editing;
using Glyph.Pdf.Pdfium;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfPageDragRegistryTests
{
    [Fact]
    public async Task Register_TryGet_returns_same_document()
    {
        var path = CreateTempPdf(pageCount: 1);
        try
        {
            var factory = new PdfiumDocumentFactory();
            await using var document = await factory.OpenAsync(path);
            PdfPageDragRegistry.Register("doc-a", document);
            PdfPageDragRegistry.TryGet("doc-a", out var resolved).Should().BeTrue();
            resolved.Should().BeSameAs(document);
        }
        finally
        {
            File.Delete(path);
            PdfPageDragRegistry.Unregister("doc-a");
        }
    }

    [Fact]
    public void Unregister_removes_entry()
    {
        PdfPageDragRegistry.Unregister("missing-key");
        PdfPageDragRegistry.TryGet("missing-key", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Unregister_after_register_prevents_lookup()
    {
        var path = CreateTempPdf(pageCount: 1);
        var key = "doc-b-" + Guid.NewGuid().ToString("N");
        try
        {
            var factory = new PdfiumDocumentFactory();
            await using var document = await factory.OpenAsync(path);
            PdfPageDragRegistry.Register(key, document);
            PdfPageDragRegistry.Unregister(key);
            PdfPageDragRegistry.TryGet(key, out _).Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTempPdf(int pageCount)
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-registry-" + Guid.NewGuid().ToString("N") + ".pdf");
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (var i = 0; i < pageCount; i++)
        {
            var page = builder.AddPage(PageSize.Letter);
            page.AddText("Page " + (i + 1), 12, new PdfPoint(72, 720), font);
        }

        File.WriteAllBytes(path, builder.Build());
        return path;
    }
}
