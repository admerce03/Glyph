using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfAnnotationColorPresetsTests
{
    [Fact]
    public void Fill_and_stroke_presets_are_stable()
    {
        PdfAnnotationColorPresets.FillChoices.Should().Contain(c => c.Name == PdfAnnotationColorPresets.TranslucentYellow);
        PdfAnnotationColorPresets.StrokePresets.Should().Contain(c => c.Name == PdfAnnotationColorPresets.DodgerBlue);
        PdfAnnotationColorPresets.FormatFillTitle("Note").Should().Be("Fill — Note");
    }
}
