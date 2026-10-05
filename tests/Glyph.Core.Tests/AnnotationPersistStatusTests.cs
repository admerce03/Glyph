using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class AnnotationPersistStatusTests
{
    [Fact]
    public void Saving_moving_and_undo_labels()
    {
        AnnotationPersistStatus.SavingInk.Should().Contain("ink");
        AnnotationPersistStatus.SavingShape.Should().Contain("shape");
        AnnotationPersistStatus.FormatMovingAnnotations(3).Should().Contain("3");
        DocumentUndoRedoStatus.NothingToUndo.Should().Contain("undo");
        DocumentUndoRedoStatus.Redoing.Should().Contain("Redoing");
        WebcamCaptureUi.StartingCamera.Should().Contain("camera");
        WebcamCaptureUi.StartingWebcam.Should().Contain("webcam");
        ContactSheetLayout.OpenHint.Should().Contain("Contact sheet");
        SidebarModeCombo.ToggleUnavailable.Should().Contain("unavailable");
    }
}
