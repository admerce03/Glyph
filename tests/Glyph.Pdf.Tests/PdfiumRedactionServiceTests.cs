using System.Text;
using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using Glyph.Pdf.Text;
using PDFiumCore;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfiumRedactionServiceTests
{
    [Fact]
    public async Task GetPending_exposes_bounds_and_label_for_preview()
    {
        var path = CreateTextPdf("Secret data here");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var redaction = new PdfiumRedactionService();
            await using var document = await factory.OpenAsync(path);

            var bounds = new PdfRect(50, 700, 200, 740);
            var mark = redaction.MarkRectangle(document, 0, bounds, "preview-label");
            var pending = redaction.GetPending(document);
            pending.Should().ContainSingle();
            var item = pending[0];
            item.Id.Should().Be(mark.Id);
            item.PageIndex.Should().Be(0);
            item.Kind.Should().Be(PdfRedactionKind.Rectangle);
            item.Bounds.Should().Be(bounds);
            item.Label.Should().Be("preview-label");
        }
        finally
        {
            File.Delete(path);
        }
    }

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
    public async Task ClearPending_removes_all_marks()
    {
        var path = CreateTextPdf("Secret data here");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var redaction = new PdfiumRedactionService();
            await using var document = await factory.OpenAsync(path);

            redaction.MarkRectangle(document, 0, new PdfRect(50, 700, 100, 740), "a");
            redaction.MarkTextRegion(document, 0, new PdfRect(120, 700, 200, 740), "b");
            redaction.GetPending(document).Should().HaveCount(2);

            redaction.ClearPending(document);
            redaction.GetPending(document).Should().BeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Apply_with_no_pending_returns_zeros()
    {
        var path = CreateTextPdf("Nothing marked");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var redaction = new PdfiumRedactionService();
            await using var document = await factory.OpenAsync(path);

            var result = await redaction.ApplyAsync(document);
            result.MarksApplied.Should().Be(0);
            result.TextObjectsRemoved.Should().Be(0);
            result.ImageObjectsRemoved.Should().Be(0);
            result.AnnotationsRemoved.Should().Be(0);
            result.AttachmentsRemoved.Should().Be(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task MarkRectangle_rejects_nonpositive_bounds()
    {
        var path = CreateTextPdf("bounds");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var redaction = new PdfiumRedactionService();
            await using var document = await factory.OpenAsync(path);

            var act = () => redaction.MarkRectangle(document, 0, new PdfRect(10, 10, 10, 20));
            act.Should().Throw<ArgumentException>().WithParameterName("bounds");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task MarkRectangle_normalizes_inverted_bounds()
    {
        var path = CreateTextPdf("invert");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var redaction = new PdfiumRedactionService();
            await using var document = await factory.OpenAsync(path);

            var mark = redaction.MarkRectangle(document, 0, new PdfRect(200, 740, 50, 700));
            mark.Bounds.Should().Be(new PdfRect(50, 700, 200, 740));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Undo_last_pending_removes_most_recent_mark()
    {
        var path = CreateTextPdf("Secret data here");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var redaction = new PdfiumRedactionService();
            await using var document = await factory.OpenAsync(path);

            var first = redaction.MarkRectangle(document, 0, new PdfRect(50, 700, 100, 740), "a");
            var second = redaction.MarkTextRegion(document, 0, new PdfRect(120, 700, 200, 740), "b");
            redaction.GetPending(document).Should().HaveCount(2);

            var undone = redaction.UndoLastPending(document);
            undone.Should().NotBeNull();
            undone!.Id.Should().Be(second.Id);
            redaction.GetPending(document).Should().ContainSingle(m => m.Id == first.Id);

            redaction.UndoLastPending(document)!.Id.Should().Be(first.Id);
            redaction.GetPending(document).Should().BeEmpty();
            redaction.UndoLastPending(document).Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Mark_text_regions_for_each_search_hit_then_apply()
    {
        var path = CreateTextPdf("alpha SECRET beta SECRET gamma");
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-redact-search-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var redaction = new PdfiumRedactionService();
            var editor = new PdfiumPageEditor();
            var search = new PdfPigTextSearchService();
            var extractor = new PdfiumTextExtractor();

            await using var document = await factory.OpenAsync(path);
            var hits = await search.SearchAsync(path, "SECRET");
            hits.Hits.Should().HaveCount(2);

            var chars = await extractor.GetCharsAsync(document, 0);
            var pageText = string.Concat(chars.Select(c => c.Value));
            var from = 0;
            while (true)
            {
                var found = pageText.IndexOf("SECRET", from, StringComparison.Ordinal);
                if (found < 0)
                {
                    break;
                }

                var end = Math.Min(chars.Count - 1, found + "SECRET".Length - 1);
                var union = chars[found].Bounds;
                for (var i = found; i <= end; i++)
                {
                    var b = chars[i].Bounds;
                    union = new PdfRect(
                        Math.Min(union.Left, b.Left),
                        Math.Min(union.Bottom, b.Bottom),
                        Math.Max(union.Right, b.Right),
                        Math.Max(union.Top, b.Top));
                }

                redaction.MarkTextRegion(document, 0, new PdfRect(union.Left - 1, union.Bottom - 1, union.Right + 1, union.Top + 1));
                from = found + 1;
            }

            redaction.GetPending(document).Should().HaveCount(2);
            var result = await redaction.ApplyAsync(document, new PdfRedactionApplyOptions(RemoveIntersectingTextObjects: true));
            result.MarksApplied.Should().Be(2);

            await editor.SaveAsync(document, outPath);
            var after = await search.SearchAsync(outPath, "SECRET");
            after.Hits.Should().BeEmpty();
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

    [Fact]
    public async Task Apply_removes_intersecting_annotations()
    {
        var path = CreateTextPdf("Visible note target");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var redaction = new PdfiumRedactionService();
            var annots = new PdfiumAnnotationService();

            await using var document = await factory.OpenAsync(path);
            await annots.AddStickyNoteAsync(
                document,
                pageIndex: 0,
                xPoints: 100,
                yPoints: 700,
                contents: "secret note",
                color: PdfAnnotationColor.StickyNoteYellow);

            var before = await annots.ListAsync(document, pageIndex: 0);
            before.Should().Contain(a => a.Contents == "secret note");

            redaction.MarkRectangle(document, 0, new PdfRect(80, 680, 160, 740), "note");
            var result = await redaction.ApplyAsync(
                document,
                new PdfRedactionApplyOptions(
                    RemoveIntersectingTextObjects: false,
                    RemoveIntersectingImageObjects: false,
                    RemoveIntersectingAnnotations: true));
            result.AnnotationsRemoved.Should().BeGreaterThan(0);

            var after = await annots.ListAsync(document, pageIndex: 0);
            after.Should().NotContain(a => a.Contents == "secret note");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Apply_removes_embedded_attachments()
    {
        var path = CreateTextPdf("Attachment host");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var redaction = new PdfiumRedactionService();
            await using var document = await factory.OpenAsync(path);
            var pdfium = (PdfiumDocument)document;

            PdfiumLibrary.EnsureInitialized();
            lock (PdfiumSync.Gate)
            {
                AddAttachment(pdfium.Handle, "secret.txt", "top secret bytes"u8.ToArray());
                fpdf_attachment.FPDFDocGetAttachmentCount(pdfium.Handle).Should().Be(1);
            }

            redaction.MarkRectangle(document, 0, new PdfRect(50, 700, 200, 740), "box");
            var result = await redaction.ApplyAsync(
                document,
                new PdfRedactionApplyOptions(
                    RemoveIntersectingTextObjects: false,
                    RemoveIntersectingImageObjects: false,
                    RemoveIntersectingAnnotations: false,
                    RemoveEmbeddedAttachments: true));
            result.AttachmentsRemoved.Should().Be(1);

            lock (PdfiumSync.Gate)
            {
                fpdf_attachment.FPDFDocGetAttachmentCount(pdfium.Handle).Should().Be(0);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Apply_clears_document_info_metadata()
    {
        var path = CreateInfoPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var redaction = new PdfiumRedactionService();
            var infoService = new PdfiumDocumentInfoService();
            await using var document = await factory.OpenAsync(path);

            var before = infoService.GetInfo(document);
            before.Title.Should().Be("Glyph Title");
            before.Author.Should().Be("Glyph Author");

            redaction.MarkRectangle(document, 0, new PdfRect(50, 700, 200, 740), "box");
            var result = await redaction.ApplyAsync(
                document,
                new PdfRedactionApplyOptions(
                    RemoveIntersectingTextObjects: false,
                    RemoveIntersectingImageObjects: false,
                    RemoveIntersectingAnnotations: false,
                    RemoveEmbeddedAttachments: false,
                    RemoveMetadata: true));
            result.MetadataCleared.Should().BeTrue();

            var after = infoService.GetInfo(document);
            after.Title.Should().BeNullOrEmpty();
            after.Author.Should().BeNullOrEmpty();
            after.Subject.Should().BeNullOrEmpty();
            after.Keywords.Should().BeNullOrEmpty();
            after.Creator.Should().BeNullOrEmpty();
            after.Producer.Should().BeNullOrEmpty();
            after.CreationDate.Should().BeNullOrEmpty();
            after.ModificationDate.Should().BeNullOrEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static unsafe void AddAttachment(FpdfDocumentT handle, string name, byte[] contents)
    {
        var nameBytes = Encoding.Unicode.GetBytes(name + "\0");
        fixed (byte* namePtr = nameBytes)
        {
            var attachment = fpdf_attachment.FPDFDocAddAttachment(handle, ref *(ushort*)namePtr);
            attachment.Should().NotBeNull();
            fixed (byte* dataPtr = contents)
            {
                fpdf_attachment.FPDFAttachmentSetFile(
                        attachment,
                        handle,
                        (IntPtr)dataPtr,
                        (uint)contents.Length)
                    .Should().NotBe(0);
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

    private static string CreateInfoPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-redact-info-" + Guid.NewGuid().ToString("N") + ".pdf");
        var builder = new PdfDocumentBuilder();
        builder.DocumentInformation.Title = "Glyph Title";
        builder.DocumentInformation.Author = "Glyph Author";
        builder.DocumentInformation.Subject = "Glyph Subject";
        builder.DocumentInformation.Keywords = "glyph, test";
        builder.DocumentInformation.Creator = "Glyph Creator";
        builder.DocumentInformation.Producer = "Glyph Producer";
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.Letter);
        page.AddText("Info sample", 14, new PdfPoint(72, 720), font);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }
}
