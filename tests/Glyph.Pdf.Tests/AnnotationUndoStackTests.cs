using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Annotations;

namespace Glyph.Pdf.Tests;

public class AnnotationUndoStackTests
{
    [Fact]
    public void Push_TryPop_are_lifo()
    {
        var stack = new AnnotationUndoStack();
        stack.Push(MakeAnnot(0, 1));
        stack.Push(MakeAnnot(2, 3));

        stack.Count.Should().Be(2);
        stack.TryPop(out var second).Should().BeTrue();
        second.PageIndex.Should().Be(2);
        second.AnnotIndex.Should().Be(3);
        stack.TryPop(out var first).Should().BeTrue();
        first.AnnotIndex.Should().Be(1);
        stack.TryPop(out _).Should().BeFalse();
    }

    [Fact]
    public void TryPopIfMatches_only_removes_matching_top()
    {
        var stack = new AnnotationUndoStack();
        stack.Push(MakeAnnot(0, 1));
        stack.Push(MakeAnnot(4, 5));

        stack.TryPopIfMatches(0, 1).Should().BeFalse();
        stack.Count.Should().Be(2);
        stack.TryPopIfMatches(4, 5).Should().BeTrue();
        stack.Count.Should().Be(1);
        stack.TryPeek(out var remaining).Should().BeTrue();
        remaining.AnnotIndex.Should().Be(1);
    }

    [Fact]
    public void Clear_empties_stack()
    {
        var stack = new AnnotationUndoStack();
        stack.Push(MakeAnnot(0, 0));
        stack.Clear();
        stack.Count.Should().Be(0);
        stack.TryPeek(out _).Should().BeFalse();
    }

    private static PdfAnnotationInfo MakeAnnot(int page, int index) =>
        new(
            PageIndex: page,
            AnnotIndex: index,
            TextMarkupKind: null,
            Bounds: new PdfRect(0, 0, 10, 10),
            Color: null,
            IsInk: true);
}
