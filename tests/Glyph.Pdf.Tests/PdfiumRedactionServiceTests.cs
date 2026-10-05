using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using Glyph.Pdf.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfiumRedactionServiceTests
{
    [Fact]
    public async Task Mark_and_remove_pending_redactions()
    {
        var path = CreateTextPdf("Secret data here");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var redaction = new PdfiumRedactionService();
            await using var document = await factory.OpenAsync(path);

            var mark = redaction.MarkRectangle(document, 0, new PdfRect(50, 700, 200, 740), "box");
            redaction.GetPending(document).Should().ContainSingle(m => m.Id == mark.Id && m.Kind == PdfRedactionKind.Rectangle);

            redaction.RemovePending(document, mark.Id).Should().BeTrue();
            redaction.GetPending(document).Should().BeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Apply_removes_searchable_text_under_redaction()
    {
        var path = CreateTextPdf("CONFIDENTIAL");
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-redact-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var redaction = new PdfiumRedactionService();
            var editor = new PdfiumPageEditor();
            var search = new PdfPigTextSearchService();

            await using var document = await factory.OpenAsync(path);
            var before = await search.SearchAsync(path, "CONFIDENTIAL");
            before.Hits.Should().NotBeEmpty();

            // Cover most of the page so glyph objects are considered intersecting.
            redaction.MarkTextRegion(document, 0, new PdfRect(36, 36, 576, 756), "all");
            var result = await redaction.ApplyAsync(document, new PdfRedactionApplyOptions(RemoveIntersectingTextObjects: true));
            result.MarksApplied.Should().Be(1);
            result.PagesChanged.Should().Be(1);
            redaction.GetPending(document).Should().BeEmpty();

            await editor.SaveAsync(document, outPath);
            var after = await search.SearchAsync(outPath, "CONFIDENTIAL");
            after.Hits.Should().BeEmpty("redaction should remove underlying extractable text");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    private static string CreateTextPdf(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-redact-" + Guid.NewGuid().ToString("N") + ".pdf");
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.Letter);
        page.AddText(text, 18, new PdfPoint(72, 720), font);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }
}
