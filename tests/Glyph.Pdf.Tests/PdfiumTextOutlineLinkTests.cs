using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using Glyph.Pdf.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfiumTextOutlineLinkTests
{
    [Fact]
    public async Task Text_extractor_returns_chars_and_copyable_text()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-text-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            CreateTextPdf(path, "Hello Glyph");
            var factory = new PdfiumDocumentFactory();
            var extractor = new PdfiumTextExtractor();
            await using var document = await factory.OpenAsync(path);

            var text = await extractor.GetTextAsync(document, 0);
            text.Should().Contain("Hello");

            var chars = await extractor.GetCharsAsync(document, 0);
            chars.Should().NotBeEmpty();
            chars.Select(c => c.Value).Should().Contain("H");

            var copied = PdfTextSelection.CopyText(chars, 0, Math.Min(4, chars.Count - 1));
            copied.Should().Contain("H");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Text_selection_by_rectangle_keeps_reading_order()
    {
        var chars = new List<PdfTextChar>
        {
            new(0, "A", new PdfRect(0, 0, 10, 10)),
            new(1, "B", new PdfRect(10, 0, 20, 10)),
            new(2, "C", new PdfRect(100, 0, 110, 10)),
        };

        PdfTextSelection.CopyCharsInRect(chars, new PdfRect(0, 0, 25, 10)).Should().Be("AB");
    }

    [Fact]
    public async Task Outline_service_reads_bookmarks()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "outline-links.pdf");
        File.Exists(path).Should().BeTrue();

        var factory = new PdfiumDocumentFactory();
        var outlines = new PdfiumOutlineService();
        await using var document = await factory.OpenAsync(path);

        var nodes = await outlines.GetOutlineAsync(document);
        nodes.Should().HaveCount(3);
        nodes.Select(n => n.Title).Should().Equal("Intro", "Details", "Appendix");
        nodes.Select(n => n.DestinationPageIndex).Should().Equal(0, 1, 2);
    }

    [Fact]
    public async Task Outline_survives_page_rotate_edit()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "outline-links.pdf");
        File.Exists(path).Should().BeTrue();

        var working = Path.Combine(Path.GetTempPath(), "glyph-outline-edit-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.Copy(path, working, overwrite: true);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var outlines = new PdfiumOutlineService();
            var editor = new PdfiumPageEditor();
            await using var document = await factory.OpenAsync(working);

            var before = await outlines.GetOutlineAsync(document);
            before.Should().HaveCount(3);

            await editor.RotatePagesAsync(document, [0], deltaDegrees: 90);

            var after = await outlines.GetOutlineAsync(document);
            after.Should().HaveCount(3);
            after.Select(n => n.Title).Should().Equal(before.Select(n => n.Title));
            after.Select(n => n.DestinationPageIndex).Should().Equal(before.Select(n => n.DestinationPageIndex));
        }
        finally
        {
            if (File.Exists(working))
            {
                File.Delete(working);
            }
        }
    }

    [Fact]
    public async Task Link_service_reads_internal_link_destination()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "outline-links.pdf");
        var factory = new PdfiumDocumentFactory();
        var links = new PdfiumLinkService();
        await using var document = await factory.OpenAsync(path);

        var pageLinks = await links.GetPageLinksAsync(document, 0);
        pageLinks.Should().NotBeEmpty();
        pageLinks.Should().Contain(l => l.DestinationPageIndex == 2);
    }

    private static void CreateTextPdf(string path, string text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.Letter);
        page.AddText(text, 18, new PdfPoint(72, 700), font);
        File.WriteAllBytes(path, builder.Build());
    }
}
