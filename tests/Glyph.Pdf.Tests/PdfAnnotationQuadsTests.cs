using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Annotations;

namespace Glyph.Pdf.Tests;

public class PdfAnnotationQuadsTests
{
    [Fact]
    public void FromChars_merges_same_line_into_one_quad()
    {
        var chars = new[]
        {
            new PdfTextChar(0, "H", new PdfRect(10, 100, 20, 112)),
            new PdfTextChar(1, "i", new PdfRect(20, 100, 28, 112)),
        };

        var quads = PdfAnnotationQuads.FromChars(chars);
        quads.Should().HaveCount(1);
        quads[0].X1.Should().BeApproximately(10, 0.01);
        quads[0].X2.Should().BeApproximately(28, 0.01);
    }

    [Fact]
    public void Serializer_round_trips_annotation_model()
    {
        var annotations = new[]
        {
            new PdfAnnotation(
                "id-1",
                PageIndex: 0,
                AnnotIndex: 0,
                PdfAnnotationKind.Highlight,
                new PdfRect(1, 2, 3, 4),
                PdfAnnotationColor.Yellow,
                Contents: "hello",
                Author: "Glyph",
                Quads: [PdfAnnotationQuads.FromRect(new PdfRect(1, 2, 3, 4))],
                SelectedText: "hello"),
        };

        var json = PdfAnnotationModelSerializer.Serialize(annotations);
        var restored = PdfAnnotationModelSerializer.Deserialize(json);

        restored.Should().BeEquivalentTo(annotations);
    }
}
