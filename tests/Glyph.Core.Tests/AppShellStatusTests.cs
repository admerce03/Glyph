using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class AppShellStatusTests
{
    [Fact]
    public void Shell_open_clipboard_and_file_op_labels()
    {
        AppShellStatus.FormatReady("Open").Should().Contain("drop files");
        AppShellStatus.RecentFilesCleared.Should().Contain("cleared");
        AppShellStatus.FormatCreatedClipboardImage("a.png").Should().Contain("a.png");
        AppShellStatus.FormatRenamedTo("b.pdf").Should().Contain("b.pdf");
        AppShellStatus.FormatRestoredTabs(3).Should().Contain("3");
        AppShellStatus.PreferencesSaved.Should().Contain("Preferences");
        AppShellStatus.FormatFailed(AppShellStatus.MoveFailedPrefix, "x").Should().Contain("Move failed: x");
        AppShellStatus.NoVersionSnapshotsYet.Should().Contain("Save");
        AppShellStatus.FormatOpenedFilesFromClipboard(2).Should().Contain("2");
        AppShellStatus.FormatActive("Doc").Should().Contain("Doc");
        AppShellStatus.RenameDialogTitle.Should().Be("Rename");
        AppShellStatus.FormatReplaceExistingBody("a.pdf").Should().Contain("a.pdf");
        AppShellStatus.FormatVersionSnapshotsTitle("X").Should().Contain("X");
        FindAllOpenPdfsStatus.DialogTitle.Should().Contain("Find");
        ScanDialogUi.NoScannersDialogTitle.Should().Be("No scanners");
        WebcamCaptureUi.CaptureDialogTitle.Should().Be("Camera capture");
    }
}
