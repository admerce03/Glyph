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

                var green = await annots.AddTextMarkupAsync(
                    document,
                    0,
                    PdfTextMarkupKind.Highlight,
                    quads,
                    PdfAnnotationColor.GreenHighlight);
                green.Color.Should().NotBeNull();
                green.Color!.Value.R.Should().Be(PdfAnnotationColor.GreenHighlight.R);

                await annots.SetColorAsync(document, 0, created.AnnotIndex, PdfAnnotationColor.PinkHighlight);

                var listed = await annots.ListAsync(document, pageIndex: 0);
                listed.Count(a => a.TextMarkupKind == PdfTextMarkupKind.Highlight).Should().Be(2);
                listed.Should().Contain(a =>
                    a.AnnotIndex == created.AnnotIndex &&
                    a.Color!.Value.R == PdfAnnotationColor.PinkHighlight.R);

                PdfAnnotationColor.HighlightPresets.Should().HaveCount(5);

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
    public async Task Add_stamp_from_bgra_lists_and_survives_save()
    {
        var path = CreateTextPdf("Stamp host page");
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-stamp-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var annots = new PdfiumAnnotationService();
            var editor = new PdfiumPageEditor();

            // Tiny opaque red BGRA square.
            const int w = 16;
            const int h = 8;
            var pixels = new byte[w * h * 4];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0;       // B
                pixels[i + 1] = 0;   // G
                pixels[i + 2] = 220; // R
                pixels[i + 3] = 255; // A
            }

            await using (var document = await factory.OpenAsync(path))
            {
                var created = await annots.AddStampAsync(
                    document,
                    0,
                    new PdfRect(72, 100, 200, 160),
                    pixels,
                    w,
                    h);
                created.IsStamp.Should().BeTrue();

                var listed = await annots.ListAsync(document, 0);
                listed.Should().Contain(a => a.IsStamp);
                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var listed = await annots.ListAsync(reopened, 0);
                listed.Should().Contain(a => a.IsStamp);
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
    public async Task Flatten_removes_editable_markup_after_bake()
    {
        var path = CreateTextPdf("Flatten host page with text");
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-flat-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var text = new PdfiumTextExtractor();
            var annots = new PdfiumAnnotationService();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var chars = await text.GetCharsAsync(document, 0);
                var quads = PdfTextMarkupQuads.FromIndexRange(chars, 0, Math.Min(4, chars.Count - 1));
                await annots.AddTextMarkupAsync(document, 0, PdfTextMarkupKind.Highlight, quads, PdfAnnotationColor.YellowHighlight);
                await annots.AddShapeAsync(
                    document,
                    0,
                    PdfShapeKind.Rectangle,
                    new PdfRect(72, 500, 180, 580),
                    new PdfAnnotationColor(30, 144, 255));

                var before = await annots.ListAsync(document, 0);
                before.Count.Should().BeGreaterThanOrEqualTo(2);

                var result = await annots.FlattenAsync(document);
                result.PagesProcessed.Should().Be(1);
                result.PagesFailed.Should().Be(0);
                result.PagesChanged.Should().BeGreaterThan(0);

                var after = await annots.ListAsync(document, 0);
                after.Should().NotContain(a => a.TextMarkupKind == PdfTextMarkupKind.Highlight);
                after.Should().NotContain(a => a.ShapeKind == PdfShapeKind.Rectangle);

                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var listed = await annots.ListAsync(reopened, 0);
                listed.Where(a => a.TextMarkupKind != null || a.ShapeKind != null).Should().BeEmpty();

                var renderer = new PdfiumRenderer();
                using var rendered = await renderer.RenderPageAsync(reopened, 0, new PdfRenderRequest(1.0));
                rendered.Width.Should().BeGreaterThan(10);
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
    public async Task Add_text_box_sets_contents_and_survives_save()
    {
        var path = CreateTextPdf("Text box host page");
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-textbox-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var annots = new PdfiumAnnotationService();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var created = await annots.AddTextBoxAsync(
                    document,
                    0,
                    new PdfRect(72, 640, 280, 720),
                    "Hello text box",
                    new PdfAnnotationColor(20, 20, 20),
                    borderColor: new PdfAnnotationColor(40, 40, 40),
                    fillColor: new PdfAnnotationColor(255, 250, 180));
                created.IsTextBox.Should().BeTrue();
                created.Contents.Should().Be("Hello text box");

                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var listed = await annots.ListAsync(reopened, 0);
                listed.Should().Contain(a => a.IsTextBox && a.Contents == "Hello text box");
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
    public async Task Add_freeform_survives_save()
    {
        var path = CreateTextPdf("Freeform host page");
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-freeform-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var annots = new PdfiumAnnotationService();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var created = await annots.AddFreeformAsync(
                    document,
                    0,
                    [
                        new PdfPagePoint(100, 100),
                        new PdfPagePoint(200, 120),
                        new PdfPagePoint(180, 200),
                        new PdfPagePoint(90, 180),
                    ],
                    new PdfAnnotationColor(40, 160, 60));
                created.ShapeKind.Should().Be(PdfShapeKind.Freeform);
                created.IsInk.Should().BeTrue();

                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var listed = await annots.ListAsync(reopened, 0);
                listed.Should().Contain(a => a.ShapeKind == PdfShapeKind.Freeform);
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
    public async Task Add_callout_survives_save_and_lists_as_callout()
    {
        var path = CreateTextPdf("Callout host page");
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-callout-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var annots = new PdfiumAnnotationService();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var created = await annots.AddCalloutAsync(
                    document,
                    0,
                    new PdfRect(200, 600, 360, 680),
                    tip: new PdfPagePoint(80, 500),
                    "Look here",
                    new PdfAnnotationColor(20, 20, 20));
                created.IsCallout.Should().BeTrue();
                created.IsTextBox.Should().BeTrue();
                created.Contents.Should().Be("Look here");

                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var listed = await annots.ListAsync(reopened, 0);
                listed.Should().Contain(a => a.IsCallout && a.Contents == "Look here");
                listed.Should().Contain(a => a.IsInk && a.Contents == "CalloutPointer");
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
    public async Task Add_rectangle_and_ellipse_survive_save()
    {
        var path = CreateTextPdf("Shape host page");
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-shape-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var annots = new PdfiumAnnotationService();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var rect = await annots.AddShapeAsync(
                    document,
                    0,
                    PdfShapeKind.Rectangle,
                    new PdfRect(72, 600, 200, 700),
                    new PdfAnnotationColor(30, 144, 255),
                    fillColor: new PdfAnnotationColor(30, 144, 255, 40));
                rect.ShapeKind.Should().Be(PdfShapeKind.Rectangle);

                var ellipse = await annots.AddShapeAsync(
                    document,
                    0,
                    PdfShapeKind.Ellipse,
                    new PdfRect(220, 620, 320, 720),
                    new PdfAnnotationColor(220, 60, 40));
                ellipse.ShapeKind.Should().Be(PdfShapeKind.Ellipse);

                var line = await annots.AddShapeAsync(
                    document,
                    0,
                    PdfShapeKind.Line,
                    new PdfRect(80, 500, 180, 560),
                    new PdfAnnotationColor(0, 128, 0));
                line.ShapeKind.Should().Be(PdfShapeKind.Line);
                line.IsInk.Should().BeTrue();

                var arrow = await annots.AddShapeAsync(
                    document,
                    0,
                    PdfShapeKind.Arrow,
                    new PdfRect(200, 480, 320, 540),
                    new PdfAnnotationColor(128, 0, 128));
                arrow.ShapeKind.Should().Be(PdfShapeKind.Arrow);
                arrow.IsInk.Should().BeTrue();
                arrow.Contents.Should().Be("Arrow");

                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var listed = await annots.ListAsync(reopened, 0);
                listed.Should().Contain(a => a.ShapeKind == PdfShapeKind.Rectangle);
                listed.Should().Contain(a => a.ShapeKind == PdfShapeKind.Ellipse);
                listed.Should().Contain(a => a.ShapeKind == PdfShapeKind.Line && a.IsInk);
                listed.Should().Contain(a => a.ShapeKind == PdfShapeKind.Arrow && a.IsInk);
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
    public async Task Add_ink_stroke_lists_and_survives_save()
    {
        var path = CreateTextPdf("Ink stroke host page");
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-ink-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var annots = new PdfiumAnnotationService();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var stroke = new List<PdfPagePoint>
                {
                    new(80, 700),
                    new(120, 720),
                    new(160, 690),
                    new(200, 710),
                };
                var created = await annots.AddInkAsync(
                    document,
                    pageIndex: 0,
                    stroke,
                    new PdfAnnotationColor(220, 60, 40));

                created.IsInk.Should().BeTrue();
                var listed = await annots.ListAsync(document, 0);
                listed.Should().Contain(a => a.IsInk);
                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var listed = await annots.ListAsync(reopened, 0);
                listed.Should().Contain(a => a.IsInk);
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
                    color: PdfAnnotationColor.StickyNoteYellow,
                    author: "Glyph Tester");

                created.IsStickyNote.Should().BeTrue();
                created.Contents.Should().Be("Hello from Glyph");
                created.Author.Should().Be("Glyph Tester");

                await annots.SetContentsAsync(document, 0, created.AnnotIndex, "Edited note");
                await annots.SetColorAsync(document, 0, created.AnnotIndex, new PdfAnnotationColor(80, 160, 255));
                await annots.MoveAsync(document, 0, created.AnnotIndex, new PdfRect(100, 650, 120, 670));

                var listed = await annots.ListAsync(document, 0);
                var note = listed.Should().ContainSingle(a => a.IsStickyNote).Subject;
                note.Contents.Should().Be("Edited note");
                note.Author.Should().Be("Glyph Tester");
                note.Bounds.Left.Should().BeApproximately(100, 0.5);

                // Resize via MoveAsync (same path as UI resize handles).
                await annots.MoveAsync(document, 0, note.AnnotIndex, new PdfRect(100, 640, 160, 700));
                listed = await annots.ListAsync(document, 0);
                note = listed.Should().ContainSingle(a => a.IsStickyNote).Subject;
                note.Bounds.Width.Should().BeApproximately(60, 0.5);
                note.Bounds.Height.Should().BeApproximately(60, 0.5);

                await annots.SetOpacityAsync(document, 0, note.AnnotIndex, opacity: 0.4f);
                listed = await annots.ListAsync(document, 0);
                note = listed.Should().ContainSingle(a => a.IsStickyNote).Subject;
                note.Color.Should().NotBeNull();
                note.Color!.Value.A.Should().Be((byte)Math.Round(0.4f * 255f));

                var copy = await annots.DuplicateAsync(document, 0, note.AnnotIndex);
                copy.IsStickyNote.Should().BeTrue();
                copy.Contents.Should().Be("Edited note");
                copy.Author.Should().Be("Glyph Tester");
                copy.Bounds.Left.Should().BeApproximately(note.Bounds.Left + 12, 0.5);
                copy.Bounds.Bottom.Should().BeApproximately(note.Bounds.Bottom - 12, 0.5);

                var afterDup = await annots.ListAsync(document, 0);
                afterDup.Count(a => a.IsStickyNote).Should().Be(2);

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
