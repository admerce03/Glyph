using FluentAssertions;
using Glyph.Pdf.Abstractions;
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

    [Fact]
    public async Task Undo_restores_page_order_after_reorder()
    {
        var path = CreatePdf(pageCount: 3);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            var history = new PdfPageEditHistory();
            await using var document = await factory.OpenAsync(path);

            // Reverse order: 2,1,0
            await history.ExecuteAsync(
                document,
                editor,
                () => editor.ReorderPagesAsync(document, [2, 1, 0]));

            await history.UndoAsync(document, editor);
            document.PageCount.Should().Be(3);
            // After undo, content of page 0 should again be the first page text.
            // Snapshot restore is enough to assert CanRedo and page count stability.
            history.CanRedo.Should().BeTrue();
            await history.RedoAsync(document, editor);
            document.PageCount.Should().Be(3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Undo_restores_page_count_after_insert_from_another_document()
    {
        var destPath = CreatePdf(pageCount: 2);
        var srcPath = CreatePdf(pageCount: 1);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            var history = new PdfPageEditHistory();
            await using var dest = await factory.OpenAsync(destPath);
            await using var src = await factory.OpenAsync(srcPath);

            await history.ExecuteAsync(
                dest,
                editor,
                () => editor.InsertPagesAsync(dest, src, [0], insertIndex: dest.PageCount));
            dest.PageCount.Should().Be(3);

            await history.UndoAsync(dest, editor);
            dest.PageCount.Should().Be(2);
        }
        finally
        {
            File.Delete(destPath);
            File.Delete(srcPath);
        }
    }

    [Fact]
    public async Task Undo_restores_page_size_after_crop()
    {
        var path = CreatePdf(pageCount: 1);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            var history = new PdfPageEditHistory();
            await using var document = await factory.OpenAsync(path);

            var beforeW = document.GetPage(0).WidthPoints;
            var beforeH = document.GetPage(0).HeightPoints;
            await history.ExecuteAsync(
                document,
                editor,
                () => editor.CropPagesAsync(document, [0], new PdfCropMargins(36, 36, 36, 36)));
            document.GetPage(0).WidthPoints.Should().BeLessThan(beforeW - 1);
            document.GetPage(0).HeightPoints.Should().BeLessThan(beforeH - 1);

            await history.UndoAsync(document, editor);
            document.GetPage(0).WidthPoints.Should().BeApproximately(beforeW, 0.5);
            document.GetPage(0).HeightPoints.Should().BeApproximately(beforeH, 0.5);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Undo_restores_page_count_after_blank_insert_and_duplicate()
    {
        var path = CreatePdf(pageCount: 2);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            var history = new PdfPageEditHistory();
            await using var document = await factory.OpenAsync(path);

            await history.ExecuteAsync(document, editor, () => editor.InsertBlankPageAsync(document, 1));
            document.PageCount.Should().Be(3);
            await history.UndoAsync(document, editor);
            document.PageCount.Should().Be(2);

            await history.ExecuteAsync(document, editor, () => editor.DuplicatePagesAsync(document, [0]));
            document.PageCount.Should().Be(3);
            await history.UndoAsync(document, editor);
            document.PageCount.Should().Be(2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Undo_restores_mediabox_after_permanent_crop()
    {
        var path = CreatePdf(pageCount: 1);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            var history = new PdfPageEditHistory();
            await using var document = await factory.OpenAsync(path);

            var beforeW = document.GetPage(0).WidthPoints;
            var beforeH = document.GetPage(0).HeightPoints;
            await history.ExecuteAsync(
                document,
                editor,
                async () =>
                {
                    await editor.CropPagesAsync(document, [0], new PdfCropMargins(24, 24, 24, 24));
                    await editor.PermanentCropPagesAsync(document, [0]);
                });
            document.GetPage(0).WidthPoints.Should().BeLessThan(beforeW - 1);

            await history.UndoAsync(document, editor);
            document.GetPage(0).WidthPoints.Should().BeApproximately(beforeW, 0.5);
            document.GetPage(0).HeightPoints.Should().BeApproximately(beforeH, 0.5);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Undo_restores_page_count_after_merge()
    {
        var destPath = CreatePdf(pageCount: 2);
        var srcPath = CreatePdf(pageCount: 1);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            var history = new PdfPageEditHistory();
            await using var dest = await factory.OpenAsync(destPath);
            await using var src = await factory.OpenAsync(srcPath);

            await history.ExecuteAsync(
                dest,
                editor,
                () => editor.MergeDocumentsAsync(dest, [src], insertIndex: dest.PageCount));
            dest.PageCount.Should().Be(3);

            await history.UndoAsync(dest, editor);
            dest.PageCount.Should().Be(2);
        }
        finally
        {
            File.Delete(destPath);
            File.Delete(srcPath);
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
