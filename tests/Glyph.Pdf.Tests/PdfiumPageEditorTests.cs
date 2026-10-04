using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfiumPageEditorTests
{
    [Fact]
    public async Task Rotate_selected_pages_updates_rotation_and_raises_pages_changed()
    {
        var path = CreateMultiPagePdf(pageCount: 3);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(path);

            var raised = 0;
            document.PagesChanged += (_, _) => raised++;

            await editor.RotatePagesAsync(document, [0, 2], deltaDegrees: 90);

            document.GetPage(0).RotationDegrees.Should().Be(90);
            document.GetPage(1).RotationDegrees.Should().Be(0);
            document.GetPage(2).RotationDegrees.Should().Be(90);
            document.GetPage(0).WidthPoints.Should().BeApproximately(document.GetPage(1).HeightPoints, 0.5);
            raised.Should().Be(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Delete_pages_removes_selected_indexes()
    {
        var path = CreateMultiPagePdf(pageCount: 4);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(path);

            await editor.DeletePagesAsync(document, [1, 3]);

            document.PageCount.Should().Be(2);
            // Remaining pages keep content from original 0 and 2.
            var renderer = new PdfiumRenderer();
            using var first = await renderer.RenderPageAsync(document, 0, new PdfRenderRequest(1.0));
            first.Width.Should().BeGreaterThan(10);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Reorder_pages_permutes_page_tree()
    {
        var path = CreateMultiPagePdf(pageCount: 3);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(path);

            await editor.ReorderPagesAsync(document, [2, 0, 1]);

            document.PageCount.Should().Be(3);
            // After reverse-ish reorder, rotations from a subsequent rotate on index 0
            // should affect the page that was originally last.
            await editor.RotatePagesAsync(document, [0], 90);
            document.GetPage(0).RotationDegrees.Should().Be(90);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Extract_pages_returns_new_document_with_subset()
    {
        var path = CreateMultiPagePdf(pageCount: 4);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(path);

            await using var extracted = await editor.ExtractPagesAsync(document, [0, 2]);
            extracted.PageCount.Should().Be(2);
            document.PageCount.Should().Be(4);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Delete_all_pages_is_rejected()
    {
        var path = CreateMultiPagePdf(pageCount: 2);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(path);

            var act = async () => await editor.DeletePagesAsync(document, [0, 1]);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Insert_blank_page_increases_count()
    {
        var path = CreateMultiPagePdf(pageCount: 2);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(path);

            await editor.InsertBlankPageAsync(document, insertIndex: 1);
            document.PageCount.Should().Be(3);
            document.GetPage(1).WidthPoints.Should().BeApproximately(612, 0.5);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Duplicate_pages_inserts_copies_after_sources()
    {
        var path = CreateMultiPagePdf(pageCount: 2);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(path);

            await editor.DuplicatePagesAsync(document, [0]);
            document.PageCount.Should().Be(3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Insert_pages_from_another_document()
    {
        var destPath = CreateMultiPagePdf(pageCount: 2);
        var srcPath = CreateMultiPagePdf(pageCount: 3);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var dest = await factory.OpenAsync(destPath);
            await using var src = await factory.OpenAsync(srcPath);

            await editor.InsertPagesAsync(dest, src, [0, 2], insertIndex: 1);
            dest.PageCount.Should().Be(4);
        }
        finally
        {
            File.Delete(destPath);
            File.Delete(srcPath);
        }
    }

    [Fact]
    public async Task Extract_and_save_round_trips_to_disk()
    {
        var path = CreateMultiPagePdf(pageCount: 3);
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-extract-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(path);
            await using var extracted = await editor.ExtractPagesAsync(document, [0, 2]);
            await editor.SaveAsync(extracted, outPath);

            File.Exists(outPath).Should().BeTrue();
            await using var reopened = await factory.OpenAsync(outPath);
            reopened.PageCount.Should().Be(2);
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    private static string CreateMultiPagePdf(int pageCount)
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-edit-" + Guid.NewGuid().ToString("N") + ".pdf");
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (var i = 0; i < pageCount; i++)
        {
            var page = builder.AddPage(PageSize.A4);
            page.AddText($"Glyph edit page {i + 1}", 18, new PdfPoint(50, 750), font);
        }

        File.WriteAllBytes(path, builder.Build());
        return path;
    }
}
