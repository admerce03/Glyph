using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class DocumentExportFormatsExtendedTests
{
    [Fact]
    public void Dpi_quality_and_transparency()
    {
        DocumentExportFormats.ParseDpi("72").Should().Be(72);
        DocumentExportFormats.ParseDpi("12").Should().Be(DocumentExportFormats.DefaultDpi);
        DocumentExportFormats.ClampQuality(0).Should().Be(1);
        DocumentExportFormats.ClampQuality(200).Should().Be(100);
        DocumentExportFormats.FlattensTransparency("JPEG").Should().BeTrue();
        DocumentExportFormats.FlattensTransparency("PNG").Should().BeFalse();
        DocumentExportFormats.ExportSummary(3).Should().Contain("3");
        DocumentExportFormats.ExportingPage.Should().Contain("Exporting");
        DocumentExportFormats.FormatExportingPages(4).Should().Contain("4 pages");
        DocumentExportFormats.FormatExportedPage(2, "a.png").Should().Contain("a.png");
        DocumentExportFormats.FormatExportedPages(3, "Out").Should().Contain("Out");
    }
}

public class CrashRecoveryPromptUiTests
{
    [Fact]
    public void Names_summary_and_status()
    {
        CrashRecoveryPromptUi.NamesSummary(["a", "b", "c", "d", "e", "f"])
            .Should().Contain("+1 more");
        CrashRecoveryPromptUi.OpenedStatus(2).Should().Contain("2");
        CrashRecoveryPromptUi.Title.Should().Contain("Recover");
        CrashRecoveryPromptUi.DiscardedStatus.Should().Contain("Discarded");
    }
}

public class DocumentKeyboardShortcutsTests
{
    [Fact]
    public void Catalog_covers_document_gestures()
    {
        DocumentKeyboardShortcuts.CatalogContains("Ctrl+P").Should().BeTrue();
        DocumentKeyboardShortcuts.CatalogContains("Ctrl+F").Should().BeTrue();
        DocumentKeyboardShortcuts.CatalogContains("F3").Should().BeTrue();
        DocumentKeyboardShortcuts.CatalogContains("Ctrl+Z").Should().BeTrue();
        DocumentKeyboardShortcuts.CatalogContains("PageDown").Should().BeTrue();
        DocumentKeyboardShortcuts.Catalog.Should().HaveCountGreaterThan(10);
    }
}

public class DocumentClosePolicyTitleTests
{
    [Fact]
    public void Unsaved_dialog_labels()
    {
        DocumentClosePolicy.UnsavedTitle.Should().Contain("Unsaved");
        DocumentClosePolicy.CloseButton.Should().Be("Close");
    }
}
