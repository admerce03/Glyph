using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Info;
using Xunit;

namespace Glyph.Pdf.Tests;

public class DocumentInfoUndoStackTests
{
    [Fact]
    public void Push_TryPop_are_lifo()
    {
        var stack = new DocumentInfoUndoStack();
        stack.Push(new PdfDocumentInfoUpdate(Title: "A"));
        stack.Push(new PdfDocumentInfoUpdate(Author: "B"));

        stack.Count.Should().Be(2);
        stack.TryPop(out var second).Should().BeTrue();
        second.Author.Should().Be("B");
        stack.TryPop(out var first).Should().BeTrue();
        first.Title.Should().Be("A");
        stack.TryPop(out _).Should().BeFalse();
    }

    [Fact]
    public void TryDiscardTop_removes_latest_without_returning()
    {
        var stack = new DocumentInfoUndoStack();
        stack.Push(new PdfDocumentInfoUpdate(Title: "Keep"));
        stack.Push(new PdfDocumentInfoUpdate(ClearAll: true));
        stack.TryDiscardTop().Should().BeTrue();
        stack.TryPop(out var remaining).Should().BeTrue();
        remaining.Title.Should().Be("Keep");
        stack.TryDiscardTop().Should().BeFalse();
    }
}
