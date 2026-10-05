using FluentAssertions;
using Glyph.Pdf.Editing;
using Glyph.Pdf.Pdfium;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfPageClipboardTests
{
    [Fact]
    public async Task Set_OpenCopy_round_trips_extracted_pages()
    {
        PdfPageClipboard.Clear();
        var path = CreateTempPdf(pageCount: 3);
        string? tempPath = null;
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(path);

            await PdfPageClipboard.SetFromDocumentAsync(editor, document, [0, 2]);
            PdfPageClipboard.HasPages.Should().BeTrue();
            PdfPageClipboard.PageCount.Should().Be(2);

            var (copy, openedPath) = await PdfPageClipboard.OpenCopyAsync(factory);
            tempPath = openedPath;
            copy.Should().NotBeNull();
            await using (copy)
            {
                copy!.PageCount.Should().Be(2);
            }
        }
        finally
        {
            PdfPageClipboard.Clear();
            File.Delete(path);
            if (tempPath is not null && File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public async Task Empty_selection_clears_clipboard()
    {
        PdfPageClipboard.Clear();
        var path = CreateTempPdf(pageCount: 1);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(path);
            await PdfPageClipboard.SetFromDocumentAsync(editor, document, [0]);
            PdfPageClipboard.HasPages.Should().BeTrue();

            await PdfPageClipboard.SetFromDocumentAsync(editor, document, []);
            PdfPageClipboard.HasPages.Should().BeFalse();
            PdfPageClipboard.PageCount.Should().Be(0);
        }
        finally
        {
            PdfPageClipboard.Clear();
            File.Delete(path);
        }
    }

    private static string CreateTempPdf(int pageCount)
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-clip-" + Guid.NewGuid().ToString("N") + ".pdf");
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
