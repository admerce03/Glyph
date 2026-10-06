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

        var orchestrator = File.ReadAllText(Path.Combine(root, InteractiveVerifyArtifacts.OrchestratorScript));
        var downloadIdx = orchestrator.IndexOf("Invoke-DownloadMsixArtifact", StringComparison.Ordinal);
        var statusOnlyIdx = orchestrator.IndexOf("if ($StatusOnly)", StringComparison.Ordinal);
        var windowsGateIdx = orchestrator.IndexOf("requires Windows for sideload", StringComparison.Ordinal);
        downloadIdx.Should().BeGreaterThan(0);
        statusOnlyIdx.Should().BeGreaterThan(downloadIdx,
            because: "-DownloadArtifact must run before -StatusOnly so Linux can prefetch then report proof");
        windowsGateIdx.Should().BeGreaterThan(statusOnlyIdx,
            because: "-StatusOnly must run before the Windows-only gate so Linux agents can report proof status");
        orchestrator.Should().Contain("-DownloadArtifact on Linux");
        orchestrator.Should().Contain("Glyph.CI.TestSign.cer");
        orchestrator.Should().Contain("gh run list");
        orchestrator.Should().Contain("gh run download $runId");
        orchestrator.Should().NotContain("gh run download --repo $Repo --branch",
            because: "gh run download has no --branch flag; pin a main run id first");
        orchestrator.Should().NotContain("if (-not $hasMsix -and $DownloadArtifact)",
            because: "-DownloadArtifact must always refresh tip packaging, not skip when a stale local MSIX exists");
        orchestrator.Should().Contain("Always refresh: a stale local layout must not skip tip packaging.");
        orchestrator.Should().Contain("gh run download errors on existing extracted files");
        orchestrator.Should().Contain("Store/production signing");
        orchestrator.Should().NotContain("ADR-015 (A/C/D)",
            because: "ADR-015 Accept A landed; orchestrator must not still escalate A/C/D");

        InteractiveVerifyArtifacts.SampleRelativePaths.Should().Equal(
            InteractiveVerifyArtifacts.SamplePdf,
            InteractiveVerifyArtifacts.SamplePng);
        foreach (var sample in InteractiveVerifyArtifacts.SampleRelativePaths)
        {
            File.Exists(Path.Combine(root, sample)).Should().BeTrue(sample);
            checklist.Should().Contain(sample, because: sample);
        }
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
