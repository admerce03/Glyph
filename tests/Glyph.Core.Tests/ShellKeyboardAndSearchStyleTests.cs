using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PdfSearchHighlightStyleTests
{
    [Fact]
    public void Gold_ARGB_matches_viewer_overlay()
    {
        PdfSearchHighlightStyle.IsGoldHighlight(90, 255, 215, 0).Should().BeTrue();
        PdfSearchHighlightStyle.IsGoldHighlight(255, 255, 215, 0).Should().BeFalse();
        PdfSearchHighlightStyle.ClearedStatus.Should().Contain("cleared");
    }
}

public class ShellKeyboardShortcutsTests
{
    [Fact]
    public void Catalog_covers_primary_shell_gestures()
    {
        ShellKeyboardShortcuts.Catalog.Should().NotBeEmpty();
        ShellKeyboardShortcuts.CatalogContains("Ctrl+O").Should().BeTrue();
        ShellKeyboardShortcuts.CatalogContains("Ctrl+S").Should().BeTrue();
        ShellKeyboardShortcuts.CatalogContains("F11").Should().BeTrue();
        ShellKeyboardShortcuts.CatalogContains("Ctrl+Shift+F").Should().BeTrue();
        ShellKeyboardShortcuts.CatalogContains("Ctrl+Shift+B").Should().BeTrue();
        ShellKeyboardShortcuts.CatalogContains("Ctrl+Shift+U").Should().BeTrue();
        ShellKeyboardShortcuts.Catalog.Should().Contain(c => c.Command == "Toggle Sidebar");
        ShellKeyboardShortcuts.Catalog.Should().Contain(c => c.Command == "Toggle Toolbar");
    }

    [Fact]
    public void MainWindow_xaml_declares_catalog_accelerators()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src/Glyph.App/MainWindow.xaml"));
        xaml.Should().Contain("Key=\"O\"");
        xaml.Should().Contain("Modifiers=\"Control\"");
        xaml.Should().Contain("Key=\"F11\"");
        xaml.Should().Contain("Key=\"F\"");
        xaml.Should().Contain("Modifiers=\"Control,Shift\"");
        ShellKeyboardShortcuts.Catalog.Should().HaveCountGreaterThan(10);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src/Glyph.App/MainWindow.xaml")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found");
    }
}
