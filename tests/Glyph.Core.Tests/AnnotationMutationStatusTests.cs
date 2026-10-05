using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

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
