using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.Pdf;

namespace Glyph.Core.Tests;

public class ShellMenuCatalogTests
{
    [Fact]
    public void MainWindow_xaml_declares_top_level_menus()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src/Glyph.App/MainWindow.xaml"));
        ShellMenuCatalog.DeclaresMenus(xaml).Should().BeTrue();
        ShellMenuCatalog.TopLevelMenus.Should().Contain("Help");
        foreach (var command in ShellMenuCatalog.FileCommands)
        {
            xaml.Should().Contain($"Text=\"{command}\"", because: command);
        }

        foreach (var command in ShellMenuCatalog.HelpCommands)
        {
            xaml.Should().Contain($"Text=\"{command}\"", because: command);
        }
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

public class PdfOutlineTreeTests
{
    [Fact]
    public void Count_and_flatten_nested_outline()
    {
        var root = new OutlineNodeLike(
            "Root",
            0,
            [
                new OutlineNodeLike("Child A", 1, []),
                new OutlineNodeLike(
                    "Child B",
                    2,
                    [new OutlineNodeLike("Grandchild", 3, [])]),
            ]);

        PdfOutlineTree.CountNodes([root]).Should().Be(4);
        PdfOutlineTree.Flatten([root]).Select(n => n.Title)
            .Should().Equal("Root", "Child A", "Child B", "Grandchild");
        PdfOutlineTree.HasNavigableDestination(root).Should().BeTrue();
        PdfOutlineTree.HasNavigableDestination(new OutlineNodeLike("x", null, [])).Should().BeFalse();
    }
}

public class WheelInputPolicyTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void PreferZoomOverScroll(bool ctrl, bool expected)
    {
        WheelInputPolicy.PreferZoomOverScroll(ctrl).Should().Be(expected);
    }
}

public class SidebarTocSearchModeTests
{
    [Fact]
    public void SidebarModeCombo_maps_toc_and_search()
    {
        SidebarModeCombo.ToSidebarMode(SidebarModeCombo.ContentsIndex)
            .Should().Be(SidebarMode.TableOfContents);
        SidebarModeCombo.ToSidebarMode(SidebarModeCombo.SearchIndex)
            .Should().Be(SidebarMode.SearchResults);
        SidebarModeCombo.FromSidebarMode(SidebarMode.TableOfContents)
            .Should().Be(SidebarModeCombo.ContentsIndex);
        SidebarModeCombo.FromSidebarMode(SidebarMode.SearchResults)
            .Should().Be(SidebarModeCombo.SearchIndex);
    }
}
