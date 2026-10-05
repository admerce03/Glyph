using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Annotations;
using Glyph.Pdf.Pdfium;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfiumAnnotationStoreTests
{
    [Fact]
    public async Task Highlight_survives_save_and_reopen()
    {
        var path = CreatePdfWithText();
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-annot-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var store = new PdfiumAnnotationStore();
            var extractor = new PdfiumTextExtractor();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var chars = await extractor.GetCharsAsync(document, 0);
                chars.Should().NotBeEmpty();
                var quads = PdfAnnotationQuads.FromChars(chars.Take(5));
                var created = await store.AddTextMarkupAsync(
                    document,
                    new PdfTextMarkupRequest(
                        0,
                        PdfAnnotationKind.Highlight,
                        quads,
                        PdfAnnotationColor.Yellow,
                        SelectedText: "Glyph",
                        Author: "Tester"));

                created.Kind.Should().Be(PdfAnnotationKind.Highlight);
                created.Quads.Should().NotBeEmpty();

                await editor.SaveAsync(document, outPath);
            }

            await using var reopened = await factory.OpenAsync(outPath);
            var listed = await store.ListAsync(reopened);
            listed.Should().ContainSingle(a => a.Kind == PdfAnnotationKind.Highlight);
            listed[0].Color.R.Should().Be(PdfAnnotationColor.Yellow.R);
            listed[0].Contents.Should().Be("Glyph");
            listed[0].Author.Should().Be("Tester");
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
    public async Task Underline_and_strikeout_can_be_listed_and_deleted()
    {
        var path = CreatePdfWithText();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var store = new PdfiumAnnotationStore();
            var extractor = new PdfiumTextExtractor();
            await using var document = await factory.OpenAsync(path);
            var chars = await extractor.GetCharsAsync(document, 0);
            var quads = PdfAnnotationQuads.FromChars(chars.Take(3));

            await store.AddTextMarkupAsync(
                document,
                new PdfTextMarkupRequest(0, PdfAnnotationKind.Underline, quads, PdfAnnotationColor.Blue));
            await store.AddTextMarkupAsync(
                document,
                new PdfTextMarkupRequest(0, PdfAnnotationKind.StrikeOut, quads, PdfAnnotationColor.Red));

            var listed = await store.ListPageAsync(document, 0);
            listed.Should().HaveCount(2);

            await store.DeleteAsync(document, 0, listed[0].AnnotIndex);
            var after = await store.ListPageAsync(document, 0);
            after.Should().HaveCount(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Sticky_note_round_trips_contents_and_color_change()
    {
        var path = CreatePdfWithText();
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-note-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var store = new PdfiumAnnotationStore();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var note = await store.AddStickyNoteAsync(
                    document,
                    new PdfStickyNoteRequest(0, 72, 720, "Remember this", PdfAnnotationColor.Pink, Author: "Glyph"));
                note.Kind.Should().Be(PdfAnnotationKind.Text);
                await store.SetColorAsync(document, note.PageIndex, note.AnnotIndex, PdfAnnotationColor.Green);
                await store.SetContentsAsync(document, note.PageIndex, note.AnnotIndex, "Updated note");
                await editor.SaveAsync(document, outPath);
            }

            await using var reopened = await factory.OpenAsync(outPath);
            var listed = await store.ListAsync(reopened);
            listed.Should().ContainSingle(a => a.Kind == PdfAnnotationKind.Text);
            listed[0].Contents.Should().Be("Updated note");
            ((int)listed[0].Color.G).Should().BeCloseTo(PdfAnnotationColor.Green.G, 2);
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
    public async Task Square_circle_freetext_and_ink_can_be_created()
    {
        var path = CreatePdfWithText();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var store = new PdfiumAnnotationStore();
            await using var document = await factory.OpenAsync(path);

            var square = await store.AddShapeAsync(
                document,
                new PdfShapeRequest(
                    0,
                    PdfAnnotationKind.Square,
                    new PdfRect(40, 600, 200, 700),
                    PdfAnnotationColor.Blue,
                    FillColor: new PdfAnnotationColor(120, 180, 255, 60)));
            square.Kind.Should().Be(PdfAnnotationKind.Square);

            var circle = await store.AddShapeAsync(
                document,
                new PdfShapeRequest(
                    0,
                    PdfAnnotationKind.Circle,
                    new PdfRect(220, 600, 320, 700),
                    PdfAnnotationColor.Red));
            circle.Kind.Should().Be(PdfAnnotationKind.Circle);

            var box = await store.AddFreeTextAsync(
                document,
                new PdfFreeTextRequest(
                    0,
                    new PdfRect(40, 500, 240, 560),
                    "Callout text",
                    PdfAnnotationColor.Black));
            box.Kind.Should().Be(PdfAnnotationKind.FreeText);
            box.Contents.Should().Be("Callout text");

            var ink = await store.AddInkAsync(
                document,
                new PdfInkRequest(
                    0,
                    [
                        [
                            new PdfUserPoint(50, 400),
                            new PdfUserPoint(80, 420),
                            new PdfUserPoint(120, 390),
                        ],
                    ],
                    PdfAnnotationColor.Red,
                    StrokeWidth: 3));
            ink.Kind.Should().Be(PdfAnnotationKind.Ink);

            var listed = await store.ListPageAsync(document, 0);
            listed.Should().HaveCount(4);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreatePdfWithText()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-annot-src-" + Guid.NewGuid().ToString("N") + ".pdf");
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("Glyph annotation sample text for markup.", 18, new PdfPoint(50, 750), font);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }
}
