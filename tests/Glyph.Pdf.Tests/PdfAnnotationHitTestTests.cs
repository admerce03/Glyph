using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Editing;

namespace Glyph.Pdf.Tests;

public class PdfAnnotationHitTestTests
{
    [Fact]
    public void HitTest_prefers_topmost_annot_on_page()
    {
        var annots = new List<PdfAnnotationInfo>
        {
            new(0, 0, PdfTextMarkupKind.Highlight, new PdfRect(10, 10, 50, 30), null),
            new(0, 1, null, new PdfRect(20, 15, 40, 25), null, IsStickyNote: true),
            new(1, 0, null, new PdfRect(10, 10, 50, 30), null, IsInk: true),
        };

        var hit = PdfAnnotationHitTest.HitTest(annots, pageIndex: 0, xPoints: 25, yPoints: 20);
        hit.Should().NotBeNull();
        hit!.AnnotIndex.Should().Be(1);
        hit.IsStickyNote.Should().BeTrue();
    }

    [Fact]
    public void HitTest_returns_null_when_miss()
    {
        var annots = new List<PdfAnnotationInfo>
        {
            new(0, 0, null, new PdfRect(10, 10, 20, 20), null, IsInk: true),
        };

        PdfAnnotationHitTest.HitTest(annots, 0, 100, 100).Should().BeNull();
    }
}
