using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfRedactionUiCopyTests
{
    [Fact]
    public void ApplyDialogTitle_warns_permanent()
    {
        PdfRedactionUiCopy.ApplyDialogTitle.Should().Be("Apply redactions permanently?");
        PdfRedactionUiCopy.ApplyDialogTitle.Should().Contain("permanently");
    }

    [Fact]
    public void FormatApplyBody_mentions_cannot_be_undone()
    {
        var body = PdfRedactionUiCopy.FormatApplyBody(3);
        body.Should().Contain("3 redaction mark");
        body.Should().Contain("cannot be undone");
    }

    [Fact]
    public void FormatPendingStatus_empty_and_nonzero()
    {
        PdfRedactionUiCopy.FormatPendingStatus(0).Should().Be("No pending redactions.");
        PdfRedactionUiCopy.FormatPendingStatus(2).Should().Contain("2 pending mark");
        PdfRedactionUiCopy.FormatPendingStatus(2).Should().Contain("cannot be undone");
    }

    [Fact]
    public void Dialog_mode_and_button_labels_are_stable()
    {
        PdfRedactionUiCopy.DialogTitle.Should().Be("Redaction");
        PdfRedactionUiCopy.ModeOff.Should().Be("Redact mode off.");
        PdfRedactionUiCopy.ModeEnter.Should().Contain("Esc to exit");
        PdfRedactionUiCopy.CancelledStatus.Should().Be("Redaction cancelled.");
        PdfRedactionUiCopy.DrawMarksButton.Should().Be("Draw marks");
        PdfRedactionUiCopy.ApplyButton.Should().Be("Apply…");
        PdfRedactionUiCopy.MarkSelectionButton.Should().Be("Mark selection");
        PdfRedactionUiCopy.MarkRegionButton.Should().Be("Mark region");
        PdfRedactionUiCopy.EmptyIntro.Should().Contain("permanently");
    }

    [Fact]
    public void ModeOffWithPending_empty_uses_ModeOff()
    {
        PdfRedactionUiCopy.ModeOffWithPending(0).Should().Be(PdfRedactionUiCopy.ModeOff);
        PdfRedactionUiCopy.ModeOffWithPending(2).Should().Contain("2 pending mark");
        PdfRedactionUiCopy.ModeOffWithPending(2).Should().Contain("Apply");
    }

    [Fact]
    public void MarkFindMatchesButton_includes_count()
    {
        PdfRedactionUiCopy.MarkFindMatchesButton(5).Should().Be("Mark find matches (5)");
    }

    [Fact]
    public void Menu_and_mark_status_helpers()
    {
        PdfRedactionUiCopy.MarkForRedactionMenu.Should().Be("Mark for redaction");
        PdfRedactionUiCopy.MarkRegionForRedactionMenu.Should().Be("Mark region for redaction");
        PdfRedactionUiCopy.FormatMarkedStatus(3).Should().Contain("3 pending");
        PdfRedactionUiCopy.FormatMarkedTextStatus(1).Should().Contain("Marked text");
        PdfRedactionUiCopy.FormatMarkedRegionStatus(2).Should().Contain("Marked region");
        PdfRedactionUiCopy.FormatMarkFailed("boom").Should().Be("Mark failed: boom");
        PdfRedactionUiCopy.MarkTooSmallStatus.Should().Contain("too small");
        PdfRedactionUiCopy.SelectTextPrompt.Should().Contain("Select text");
        PdfRedactionUiCopy.DragRegionPrompt.Should().Contain("region");
    }

    [Fact]
    public void Remove_undo_apply_status_helpers()
    {
        PdfRedactionUiCopy.FormatRemovedStatus(0).Should().Be(PdfRedactionUiCopy.RemovedMarkStatus);
        PdfRedactionUiCopy.FormatRemovedStatus(4).Should().Contain("4 pending");
        PdfRedactionUiCopy.FormatUndidStatus(0).Should().Be(PdfRedactionUiCopy.UndidMarkStatus);
        PdfRedactionUiCopy.FormatUndidStatus(1).Should().Contain("1 remaining");
        PdfRedactionUiCopy.NothingToApplyStatus.Should().Be("Nothing to apply.");
        PdfRedactionUiCopy.FormatApplyFailed("x").Should().Be("Apply failed: x");
        PdfRedactionUiCopy.ClearedPendingStatus.Should().Contain("Cleared");
        PdfRedactionUiCopy.ApplyingStatus.Should().Contain("Applying");

        var applied = PdfRedactionUiCopy.FormatAppliedStatus(2, 1, 3, 1, 0, 0, metadataCleared: true);
        applied.Should().Contain("2 redaction");
        applied.Should().Contain("metadata cleared");
        applied.Should().Contain("3 text");
    }

    [Fact]
    public void Tooltips_warn_permanent_and_pending_remove()
    {
        PdfRedactionUiCopy.ToolbarTooltip.Should().Contain("permanently");
        PdfRedactionUiCopy.PendingOverlayTooltip.Should().Contain("remove");
    }
}
