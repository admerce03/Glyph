using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using Glyph.Pdf.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfiumAnnotationServiceTests
{
    [Fact]
    public async Task Add_highlight_lists_and_survives_save_reopen()
    {
        var path = CreateTextPdf("Glyph annotation highlight sample");
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-annot-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var text = new PdfiumTextExtractor();
            var annots = new PdfiumAnnotationService();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var chars = await text.GetCharsAsync(document, 0);
                chars.Should().NotBeEmpty();
                var quads = PdfTextMarkupQuads.FromIndexRange(chars, 0, Math.Min(5, chars.Count - 1));
                quads.Should().NotBeEmpty();

                var created = await annots.AddTextMarkupAsync(
                    document,
                    pageIndex: 0,
                    PdfTextMarkupKind.Highlight,
                    quads,
                    PdfAnnotationColor.YellowHighlight);

                created.TextMarkupKind.Should().Be(PdfTextMarkupKind.Highlight);
                created.PageIndex.Should().Be(0);

                var listed = await annots.ListAsync(document, pageIndex: 0);
                listed.Should().ContainSingle(a => a.TextMarkupKind == PdfTextMarkupKind.Highlight);

                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var listed = await annots.ListAsync(reopened, pageIndex: 0);
                listed.Should().Contain(a => a.TextMarkupKind == PdfTextMarkupKind.Highlight);
            }
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
    public async Task Add_underline_and_strikeout_and_remove()
    {
        var path = CreateTextPdf("Underline and strike sample text");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var text = new PdfiumTextExtractor();
            var annots = new PdfiumAnnotationService();
            await using var document = await factory.OpenAsync(path);
            var chars = await text.GetCharsAsync(document, 0);
            var quads = PdfTextMarkupQuads.FromIndexRange(chars, 0, Math.Min(3, chars.Count - 1));

            await annots.AddTextMarkupAsync(document, 0, PdfTextMarkupKind.Underline, quads, PdfAnnotationColor.UnderlineBlue);
            await annots.AddTextMarkupAsync(document, 0, PdfTextMarkupKind.StrikeOut, quads, PdfAnnotationColor.StrikeOutRed);

            var listed = await annots.ListAsync(document, 0);
            listed.Count(a => a.TextMarkupKind is PdfTextMarkupKind.Underline or PdfTextMarkupKind.StrikeOut)
                .Should()
                .Be(2);

            var strike = listed.First(a => a.TextMarkupKind == PdfTextMarkupKind.StrikeOut);
            await annots.RemoveAsync(document, strike.PageIndex, strike.AnnotIndex);

            var after = await annots.ListAsync(document, 0);
            after.Should().NotContain(a => a.TextMarkupKind == PdfTextMarkupKind.StrikeOut);
            after.Should().Contain(a => a.TextMarkupKind == PdfTextMarkupKind.Underline);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Markup_quads_merge_same_line_chars()
    {
        var chars = new List<PdfTextChar>
        {
            new(0, "A", new PdfRect(10, 20, 18, 32)),
            new(1, "B", new PdfRect(18, 20, 26, 32)),
            new(2, "C", new PdfRect(10, 40, 18, 52)),
        };

        var quads = PdfTextMarkupQuads.FromChars(chars);
        quads.Should().HaveCount(2);
        quads[0].Bounds.Left.Should().BeApproximately(10, 0.01);
        quads[0].Bounds.Right.Should().BeApproximately(26, 0.01);
        quads[1].Bounds.Bottom.Should().BeApproximately(40, 0.01);
    }

    [Fact]
    public async Task Add_sticky_note_sets_contents_color_and_survives_save()
    {
        var path = CreateTextPdf("Sticky note host page");
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-note-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var annots = new PdfiumAnnotationService();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var created = await annots.AddStickyNoteAsync(
                    document,
                    pageIndex: 0,
                    xPoints: 72,
                    yPoints: 700,
                    contents: "Hello from Glyph",
                    color: PdfAnnotationColor.StickyNoteYellow);

                created.IsStickyNote.Should().BeTrue();
                created.Contents.Should().Be("Hello from Glyph");

                await annots.SetContentsAsync(document, 0, created.AnnotIndex, "Edited note");
                await annots.SetColorAsync(document, 0, created.AnnotIndex, new PdfAnnotationColor(80, 160, 255));
                await annots.MoveAsync(document, 0, created.AnnotIndex, new PdfRect(100, 650, 120, 670));

                var listed = await annots.ListAsync(document, 0);
                var note = listed.Should().ContainSingle(a => a.IsStickyNote).Subject;
                note.Contents.Should().Be("Edited note");
                note.Bounds.Left.Should().BeApproximately(100, 0.5);

                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var listed = await annots.ListAsync(reopened, 0);
                listed.Should().Contain(a => a.IsStickyNote && a.Contents == "Edited note");
            }
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

    private static string CreateTextPdf(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-annot-" + Guid.NewGuid().ToString("N") + ".pdf");
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText(text, 18, new PdfPoint(50, 750), font);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }
}
