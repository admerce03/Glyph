using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Editing;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfAnnotationListLabelTests
{
    [Fact]
    public void Format_sticky_note_includes_author_and_preview()
    {
        var info = new PdfAnnotationInfo(
            2, 1, null, new PdfRect(0, 0, 10, 10), null,
            Contents: "Hello\nworld",
            IsStickyNote: true, Author: "Ada");
        PdfAnnotationListLabel.Format(info).Should().Be("Note · Ada · p.3: Hello world");
    }

    [Fact]
    public void Format_callout_marks_underline_and_empty()
    {
        var empty = new PdfAnnotationInfo(
            0, 0, null, new PdfRect(0, 0, 1, 1), null,
            Contents: "  ",
            IsCallout: true, IsUnderlined: true);
        PdfAnnotationListLabel.Format(empty).Should().Be("Callout · U · p.1: (empty)");
    }

    [Fact]
    public void Format_shape_and_ink_and_markup()
    {
        PdfAnnotationListLabel.Format(new PdfAnnotationInfo(
            0, 0, null, new PdfRect(0, 0, 1, 1), null,
            ShapeKind: PdfShapeKind.Arrow)).Should().Be("Arrow · p.1");

        PdfAnnotationListLabel.Format(new PdfAnnotationInfo(
            4, 0, null, new PdfRect(0, 0, 1, 1), null,
            IsInk: true)).Should().Be("Ink · p.5");

        PdfAnnotationListLabel.Format(new PdfAnnotationInfo(
            0, 0, PdfTextMarkupKind.StrikeOut, new PdfRect(0, 0, 1, 1), null))
            .Should().Be("Strike · p.1");
    }

    [Fact]
    public void Format_group_prefix_and_long_preview_truncation()
    {
        var longText = new string('x', 50);
        var info = new PdfAnnotationInfo(
            0, 0, null, new PdfRect(0, 0, 1, 1), null,
            Contents: longText,
            IsTextBox: true, GroupId: "g1");
        var label = PdfAnnotationListLabel.Format(info);
        label.Should().StartWith("[G] Text · p.1: ");
        label.Should().EndWith("…");
        label.Length.Should().Be("[G] Text · p.1: ".Length + 43); // 42 chars + ellipsis
    }

    [Fact]
    public void TrimPreview_flattens_newlines()
    {
        PdfAnnotationListLabel.TrimPreview("a\nb\rc").Should().Be("a b c");
    }
}
