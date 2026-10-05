using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.Printing;

namespace Glyph.Core.Tests;

public class ScanDialogUiExtendedTests
{
    [Fact]
    public void Paper_crop_and_duplex_labels()
    {
        ScanDialogUi.PaperSizeLabels.Should().Contain("A4");
        ScanDialogUi.AutoCropLabels.Should().Contain("Single region");
        ScanDialogUi.DuplexLabel.Should().Contain("Duplex");
        ScanDialogUi.DestinationLabels.Should().Contain("Insert into current PDF");
        ScanDialogUi.LookingForScanners.Should().Contain("Looking");
        ScanDialogUi.FormatScanningWith("X").Should().Contain("X");
        ScanDialogUi.FormatFailed("e").Should().Contain("failed: e");
        ScanDialogUi.FormatInsertedPages(2).Should().Contain("2");
    }
}

public class PrintPageScopeChooserTests
{
    [Fact]
    public void Scope_labels()
    {
        PrintPageScopeChooser.Labels.Should().HaveCount(4);
        PrintPageScopeChooser.FromComboIndex(0).Should().Be(PrintPageScopeChooser.Scope.CurrentPage);
        PrintPageScopeChooser.FromComboIndex(3).Should().Be(PrintPageScopeChooser.Scope.AllPages);
        PrintPageScopeChooser.CancelledStatus.Should().Contain("cancelled");
        PrintPageScopeChooser.FormatPrintUiShown(3, 1).Should().Contain("3 page");
        PrintPageScopeChooser.FormatPrintUiShown(3, 2).Should().Contain("2-up");
    }
}
