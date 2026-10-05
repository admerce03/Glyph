using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Forms;
using Xunit;

namespace Glyph.Pdf.Tests;

public class FormFillUndoStackTests
{
    [Fact]
    public void Push_and_TryPop_are_lifo()
    {
        var stack = new FormFillUndoStack();
        stack.Push(new FormFillUndoEntry(0, 1, PdfFormFieldKind.TextField, "a"));
        stack.Push(new FormFillUndoEntry(2, 3, PdfFormFieldKind.CheckBox, "Off"));

        stack.Count.Should().Be(2);
        stack.TryPop(out var second).Should().BeTrue();
        second.PageIndex.Should().Be(2);
        second.AnnotIndex.Should().Be(3);
        second.Kind.Should().Be(PdfFormFieldKind.CheckBox);
        second.PreviousValue.Should().Be("Off");

        stack.TryPop(out var first).Should().BeTrue();
        first.PreviousValue.Should().Be("a");
        stack.Count.Should().Be(0);
        stack.TryPop(out _).Should().BeFalse();
    }

    [Fact]
    public void Push_from_field_info_captures_empty_value_as_empty_string()
    {
        var stack = new FormFillUndoStack();
        var field = new PdfFormFieldInfo(
            PageIndex: 1,
            AnnotIndex: 4,
            Name: "Email",
            Kind: PdfFormFieldKind.TextField,
            Value: string.Empty,
            Bounds: new PdfRect(0, 0, 10, 10),
            TabOrder: 0);

        stack.Push(field);
        stack.TryPop(out var entry).Should().BeTrue();
        entry.PageIndex.Should().Be(1);
        entry.AnnotIndex.Should().Be(4);
        entry.PreviousValue.Should().BeEmpty();
    }

    [Fact]
    public void Clear_empties_stack()
    {
        var stack = new FormFillUndoStack();
        stack.Push(new FormFillUndoEntry(0, 0, PdfFormFieldKind.TextField, "x"));
        stack.Clear();
        stack.Count.Should().Be(0);
        stack.TryPop(out _).Should().BeFalse();
    }
}
