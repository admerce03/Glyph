using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class AnnotationMutationStatusTests
{
    [Fact]
    public void Mutation_result_labels()
    {
        AnnotationMutationStatus.ColorUpdated.Should().Contain("color");
        AnnotationMutationStatus.Resized.Should().Contain("resized");
        AnnotationMutationStatus.Rotated90.Should().Contain("90");
        AnnotationMutationStatus.EditCancelled.Should().Contain("cancelled");
        AnnotationMutationStatus.FormatAuthorSet("Ada").Should().Contain("Ada");
        AnnotationMutationStatus.FormatErased("Ink").Should().Contain("Erased");
        AnnotationMutationStatus.FormatDuplicated("Note").Should().Contain("Duplicated");
        AnnotationMutationStatus.FillCleared.Should().Be("Fill cleared.");
        AnnotationMutationStatus.FormatFlattenedAnnotations(1).Should().Contain("1 page");
        AnnotationMutationStatus.FormatDeletedAnnotation("Note").Should().StartWith("Deleted");
        AnnotationMutationStatus.FormatChromeStatus(1, 10, 100, "Continuous", "").Should().Contain("Zoom 100%");
        PageEditStatus.CropCancelled.Should().Contain("Crop");
        PageEditStatus.MergeCancelled.Should().Contain("Merge");
        WebcamCaptureUi.CameraUiUnavailable.Should().Contain("unavailable");
        PrintPageScopeChooser_NoPages();
    }

    private static void PrintPageScopeChooser_NoPages()
    {
        Glyph.Core.Printing.PrintPageScopeChooser.NoPagesToPrint.Should().Contain("print");
    }
}
