using FluentAssertions;
using Glyph.Pdf.Editing;
using Glyph.Pdf.Pdfium;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfPageEditHistoryTests
{
    [Fact]
    public async Task Undo_restores_page_count_after_delete()
    {
        var path = CreatePdf(pageCount: 3);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            var history = new PdfPageEditHistory();
            await using var document = await factory.OpenAsync(path);

            await history.ExecuteAsync(document, editor, () => editor.DeletePagesAsync(document, [1]));
            document.PageCount.Should().Be(2);
            history.CanUndo.Should().BeTrue();

            await history.UndoAsync(document, editor);
            document.PageCount.Should().Be(3);
            history.CanRedo.Should().BeTrue();

            await history.RedoAsync(document, editor);
            document.PageCount.Should().Be(2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Undo_restores_rotation()
    {
        var path = CreatePdf(pageCount: 1);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            var history = new PdfPageEditHistory();
            await using var document = await factory.OpenAsync(path);

            await history.ExecuteAsync(document, editor, () => editor.RotatePagesAsync(document, [0], 90));
            document.GetPage(0).RotationDegrees.Should().Be(90);

            await history.UndoAsync(document, editor);
            document.GetPage(0).RotationDegrees.Should().Be(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreatePdf(int pageCount)
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-undo-" + Guid.NewGuid().ToString("N") + ".pdf");
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (var i = 0; i < pageCount; i++)
        {
            var page = builder.AddPage(PageSize.A4);
            page.AddText($"undo {i + 1}", 18, new PdfPoint(50, 750), font);
        }

        File.WriteAllBytes(path, builder.Build());
        return path;
    }
}
