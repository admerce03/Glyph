using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class InteractiveVerifyArtifactsTests
{
    [Fact]
    public void Expected_paths_match_interactive_verify_doc()
    {
        InteractiveVerifyArtifacts.ProofDirectory.Should().Be("docs/proof");
        InteractiveVerifyArtifacts.ExpectedRelativePaths.Should().Equal(
            InteractiveVerifyArtifacts.M1ShellTabs,
            InteractiveVerifyArtifacts.M2PdfViewer,
            InteractiveVerifyArtifacts.M3PageDnD);

        var root = FindRepoRoot();
        var checklist = File.ReadAllText(Path.Combine(root, InteractiveVerifyArtifacts.ChecklistDoc));
        foreach (var path in InteractiveVerifyArtifacts.ExpectedRelativePaths)
        {
            checklist.Should().Contain(path, because: path);
        }

        checklist.Should().Contain("interactive-verify.ps1");
        Directory.Exists(Path.Combine(root, InteractiveVerifyArtifacts.ProofDirectory)).Should().BeTrue();
        File.Exists(Path.Combine(root, InteractiveVerifyArtifacts.ProofDirectory, "README.md")).Should().BeTrue();
        File.Exists(Path.Combine(root, InteractiveVerifyArtifacts.OrchestratorScript)).Should().BeTrue();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, InteractiveVerifyArtifacts.ChecklistDoc)))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found");
    }
}
