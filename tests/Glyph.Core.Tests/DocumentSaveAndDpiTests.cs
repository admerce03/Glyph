using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.Platform;

namespace Glyph.Core.Tests;

public class DocumentSaveStatusTests
{
    [Fact]
    public void Saved_marks_read_only()
    {
        DocumentSaveStatus.Saved("a.pdf", isReadOnly: false).Should().Be("Saved a.pdf");
        DocumentSaveStatus.Saved("a.pdf", isReadOnly: true).Should().Contain("read-only");
        DocumentSaveStatus.NoDocument.Should().Contain("save");
    }
}

public class DpiAwarenessDeclarationTests
{
    [Fact]
    public void App_manifest_declares_PerMonitorV2()
    {
        var root = FindRepoRoot();
        var manifestPath = Path.Combine(root, DpiAwarenessDeclaration.ManifestRelativePath);
        File.Exists(manifestPath).Should().BeTrue(manifestPath);
        var xml = File.ReadAllText(manifestPath);
        DpiAwarenessDeclaration.ManifestDeclaresPerMonitorV2(xml).Should().BeTrue();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Glyph.sln"))
                || File.Exists(Path.Combine(dir.FullName, DpiAwarenessDeclaration.ManifestRelativePath)))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found from " + AppContext.BaseDirectory);
    }
}
