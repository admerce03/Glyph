using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class AppShellTextLabelsTests
{
    [Fact]
    public void Labels_are_stable()
    {
        AppShellTextLabels.CloseTab.Should().Be("Close Tab");
        AppShellTextLabels.MoveToNewWindow.Should().Be("Move to New Window");
        AppShellTextLabels.StatusChromeSubtitle.Should().Be("Native Windows document preview");
        AppShellTextLabels.StatusChromeSubtitle.Should().NotContain("Milestone");
    }

    [Fact]
    public void MainWindow_status_chrome_matches_product_subtitle()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src/Glyph.App/MainWindow.xaml"));
        xaml.Should().Contain($"Text=\"{AppShellTextLabels.StatusChromeSubtitle}\"");
        xaml.Should().NotContain("Milestone 2");
        xaml.Should().NotContain("Milestone 2 PDF viewer");
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
