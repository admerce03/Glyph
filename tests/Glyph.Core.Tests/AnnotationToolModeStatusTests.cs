using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class AnnotationToolModeStatusTests
{
    [Fact]
    public void Highlight_ink_eraser_labels()
    {
        AnnotationToolModeStatus.HighlightOff.Should().Contain("off");
        AnnotationToolModeStatus.HighlightOn.Should().Contain("Esc");
        AnnotationToolModeStatus.InkOn.Should().Contain("draw");
        AnnotationToolModeStatus.EraserOn.Should().Contain("annotation");
        AnnotationToolModeStatus.EraserMiss.Should().Contain("no annotation");
    }

    [Fact]
    public void Callout_freeform_polygon_shape_labels()
    {
        AnnotationToolModeStatus.CalloutOn.Should().Contain("drag");
        AnnotationToolModeStatus.CalloutAdded.Should().Be("Callout added.");
        AnnotationToolModeStatus.FreeformOn.Should().Contain("closed shape");
        AnnotationToolModeStatus.PolygonOn.Should().Contain("vertices");
        AnnotationToolModeStatus.PolygonNeedsVertices.Should().Contain("three");
        AnnotationToolModeStatus.ShapeOff.Should().Contain("off");
        AnnotationToolModeStatus.MagnifierOn.Should().Contain("Esc");
        AnnotationToolModeStatus.CropOn.Should().Contain("Enter");
        AnnotationToolModeStatus.ExitedPresentation.Should().Contain("presentation");
    }
}
