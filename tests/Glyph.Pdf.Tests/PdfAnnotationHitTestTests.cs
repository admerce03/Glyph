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

    [Fact]
    public void SameIdentity_matches_page_and_annot_index()
    {
        var a = new PdfAnnotationInfo(1, 2, null, new PdfRect(0, 0, 1, 1), null);
        var b = new PdfAnnotationInfo(1, 2, null, new PdfRect(9, 9, 10, 10), null, IsInk: true);
        var c = new PdfAnnotationInfo(1, 3, null, new PdfRect(0, 0, 1, 1), null);
        PdfAnnotationHitTest.SameIdentity(a, b).Should().BeTrue();
        PdfAnnotationHitTest.SameIdentity(a, c).Should().BeFalse();
    }

    [Fact]
    public void HitTestWithPad_hits_inside_pad_outside_bounds()
    {
        var annots = new List<PdfAnnotationInfo>
        {
            new(0, 0, null, new PdfRect(10, 10, 20, 20), null, IsInk: true),
        };

        // Point is 4 units left of bounds; pad=8 should hit.
        var hit = PdfAnnotationHitTest.HitTestWithPad(annots, xPoints: 6, yPoints: 15, pad: 8);
        hit.Should().NotBeNull();
        hit!.AnnotIndex.Should().Be(0);
    }

    [Fact]
    public void HitTestWithPad_misses_beyond_pad()
    {
        var annots = new List<PdfAnnotationInfo>
        {
            new(0, 0, null, new PdfRect(10, 10, 20, 20), null, IsInk: true),
        };

        PdfAnnotationHitTest.HitTestWithPad(annots, xPoints: 1, yPoints: 15, pad: 8).Should().BeNull();
    }

    [Fact]
    public void HitTestWithPad_prefers_topmost_among_overlaps()
    {
        var annots = new List<PdfAnnotationInfo>
        {
            new(0, 0, null, new PdfRect(10, 10, 30, 30), null, IsInk: true),
            new(0, 2, null, new PdfRect(12, 12, 28, 28), null, IsInk: true),
        };

        var hit = PdfAnnotationHitTest.HitTestWithPad(annots, xPoints: 20, yPoints: 20, pad: 2);
        hit!.AnnotIndex.Should().Be(2);
    }

    [Fact]
    public void HitTestWithPad_handles_inverted_bounds()
    {
        var annots = new List<PdfAnnotationInfo>
        {
            new(0, 0, null, new PdfRect(20, 20, 10, 10), null, IsInk: true),
        };

        // Normalized box is [10,10]–[20,20]; pad=4 → [6,6]–[24,24].
        PdfAnnotationHitTest.HitTestWithPad(annots, xPoints: 15, yPoints: 15, pad: 0).Should().NotBeNull();
        PdfAnnotationHitTest.HitTestWithPad(annots, xPoints: 6, yPoints: 15, pad: 4).Should().NotBeNull();
        PdfAnnotationHitTest.HitTestWithPad(annots, xPoints: 5, yPoints: 15, pad: 4).Should().BeNull();
    }
}
