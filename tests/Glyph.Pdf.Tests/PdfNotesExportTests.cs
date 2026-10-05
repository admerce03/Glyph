using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Editing;

namespace Glyph.Pdf.Tests;

public class PdfNotesExportTests
{
    [Fact]
    public void Format_includes_sticky_notes_in_page_order()
    {
        var annots = new[]
        {
            new PdfAnnotationInfo(1, 0, null, new PdfRect(0, 0, 10, 10), null, "Second page note", IsStickyNote: true, Author: "Ada"),
            new PdfAnnotationInfo(0, 2, null, new PdfRect(0, 0, 10, 10), null, "First note", IsStickyNote: true),
            new PdfAnnotationInfo(0, 1, PdfTextMarkupKind.Highlight, new PdfRect(0, 0, 10, 10), null, "ignored highlight"),
            new PdfAnnotationInfo(0, 3, null, new PdfRect(0, 0, 10, 10), null, "   ", IsStickyNote: true),
        };

        var text = PdfNotesExport.Format(
            annots,
            documentTitle: "Sample.pdf",
            generatedAt: new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));

        text.Should().Contain("Document: Sample.pdf");
        text.Should().Contain("Notes: 3");
        text.Should().Contain("Note 1 — page 1");
        text.Should().Contain("First note");
        text.Should().Contain("Note 2 — page 1");
        text.Should().Contain("(empty)");
        text.Should().Contain("Note 3 — page 2 — Ada");
        text.Should().Contain("Second page note");
        text.Should().NotContain("ignored highlight");
    }

    [Fact]
    public void Format_empty_document_reports_no_notes()
    {
        var text = PdfNotesExport.Format(
            Array.Empty<PdfAnnotationInfo>(),
            documentTitle: "Empty.pdf",
            generatedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        text.Should().Contain("Notes: 0");
        text.Should().Contain("(No sticky notes.)");
    }
}
