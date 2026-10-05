using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class ChromeAutomationNamesTests
{
    [Fact]
    public void MainWindow_xaml_declares_required_names()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src/Glyph.App/MainWindow.xaml"));
        foreach (var name in ChromeAutomationNames.RequiredNames)
        {
            xaml.Should().Contain($"AutomationProperties.Name=\"{name}\"", because: name);
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

public class DocumentExportFormatsTests
{
    [Fact]
    public void PageImageFormatNames_map_to_extensions()
    {
        DocumentExportFormats.PageImageFormatNames.Should().Contain("PNG");
        DocumentExportFormats.PageImageFormatNames.Should().Contain("JPEG 2000");
        DocumentExportFormats.ExtensionForDisplayName("JPEG").Should().Be(".jpg");
        DocumentExportFormats.ExtensionForDisplayName("JPEG 2000").Should().Be(".jp2");
        DocumentExportFormats.ExtensionForDisplayName("PNG").Should().Be(".png");
        DocumentExportFormats.CancelledStatus.Should().Contain("cancelled");
    }
}
