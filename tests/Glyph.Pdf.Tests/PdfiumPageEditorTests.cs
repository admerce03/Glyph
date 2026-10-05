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
    public async Task Insert_pages_at_zero_prepends()
    {
        var destPath = CreateMultiPagePdf(pageCount: 2);
        var srcPath = CreateMultiPagePdf(pageCount: 1);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var dest = await factory.OpenAsync(destPath);
            await using var src = await factory.OpenAsync(srcPath);

            await editor.InsertPagesAsync(dest, src, [0], insertIndex: 0);
            dest.PageCount.Should().Be(3);
        }
        finally
        {
            File.Delete(destPath);
            File.Delete(srcPath);
        }
    }

    [Fact]
    public async Task Insert_pages_at_page_count_appends()
    {
        var destPath = CreateMultiPagePdf(pageCount: 2);
        var srcPath = CreateMultiPagePdf(pageCount: 1);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var dest = await factory.OpenAsync(destPath);
            await using var src = await factory.OpenAsync(srcPath);

            await editor.InsertPagesAsync(dest, src, [0], insertIndex: dest.PageCount);
            dest.PageCount.Should().Be(3);
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

    [Fact]
    public async Task Crop_pages_sets_non_destructive_cropbox_and_shrinks_page_size()
    {
        var path = CreateMultiPagePdf(pageCount: 2);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(path);

            var beforeWidth = document.GetPage(0).WidthPoints;
            var beforeHeight = document.GetPage(0).HeightPoints;
            var margins = new PdfCropMargins(36, 48, 36, 48);

            await editor.CropPagesAsync(document, [0], margins);

            document.GetPage(0).WidthPoints.Should().BeApproximately(beforeWidth - 72, 1.0);
            document.GetPage(0).HeightPoints.Should().BeApproximately(beforeHeight - 96, 1.0);
            // Uncropped page keeps original size.
            document.GetPage(1).WidthPoints.Should().BeApproximately(beforeWidth, 1.0);
            document.GetPage(1).HeightPoints.Should().BeApproximately(beforeHeight, 1.0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SetCropBox_applies_same_box_to_multiple_pages()
    {
        var path = CreateMultiPagePdf(pageCount: 3);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(path);

            var box = new PdfCropBox(20, 40, 400, 600);
            await editor.SetCropBoxAsync(document, [0, 2], box);

            document.GetPage(0).WidthPoints.Should().BeApproximately(380, 1.0);
            document.GetPage(0).HeightPoints.Should().BeApproximately(560, 1.0);
            document.GetPage(2).WidthPoints.Should().BeApproximately(380, 1.0);
            document.GetPage(2).HeightPoints.Should().BeApproximately(560, 1.0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Permanent_crop_sets_mediabox_to_cropbox_and_round_trips()
    {
        var path = CreateMultiPagePdf(pageCount: 1);
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-perm-crop-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(path);

            await editor.CropPagesAsync(document, [0], new PdfCropMargins(36, 36, 36, 36));
            var croppedWidth = document.GetPage(0).WidthPoints;
            var croppedHeight = document.GetPage(0).HeightPoints;

            await using var extracted = await editor.ExtractPagesAsync(document, [0]);
            await editor.PermanentCropPagesAsync(extracted, [0]);
            await editor.SaveAsync(extracted, outPath);

            await using var reopened = await factory.OpenAsync(outPath);
            reopened.PageCount.Should().Be(1);
            reopened.GetPage(0).WidthPoints.Should().BeApproximately(croppedWidth, 1.0);
            reopened.GetPage(0).HeightPoints.Should().BeApproximately(croppedHeight, 1.0);
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

    [Fact]
    public async Task Merge_documents_appends_all_source_pages()
    {
        var destPath = CreateMultiPagePdf(pageCount: 2);
        var srcA = CreateMultiPagePdf(pageCount: 1);
        var srcB = CreateMultiPagePdf(pageCount: 2);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var dest = await factory.OpenAsync(destPath);
            await using var a = await factory.OpenAsync(srcA);
            await using var b = await factory.OpenAsync(srcB);

            await editor.MergeDocumentsAsync(dest, [a, b], insertIndex: dest.PageCount);
            dest.PageCount.Should().Be(5);
        }
        finally
        {
            File.Delete(destPath);
            File.Delete(srcA);
            File.Delete(srcB);
        }
    }

    [Fact]
    public async Task Split_document_returns_contiguous_parts()
    {
        var path = CreateMultiPagePdf(pageCount: 5);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(path);

            var parts = await editor.SplitDocumentAsync(document, [2, 4]);
            try
            {
                parts.Should().HaveCount(3);
                parts[0].PageCount.Should().Be(2);
                parts[1].PageCount.Should().Be(2);
                parts[2].PageCount.Should().Be(1);
            }
            finally
            {
                foreach (var part in parts)
                {
                    await part.DisposeAsync();
                }
            }
        }
        finally
        {
            File.Delete(path);
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
