namespace Glyph.Core.Documents;

/// <summary>
/// Expected artifact paths for Windows interactive verify (M9 / INTERACTIVE_VERIFY.md).
/// Operator entrypoint: <c>scripts/interactive-verify.ps1</c>.
/// </summary>
public static class InteractiveVerifyArtifacts
{
    public const string ProofDirectory = "docs/proof";

    public const string M1ShellTabs = "docs/proof/m1-shell-tabs.png";
    public const string M2PdfViewer = "docs/proof/m2-pdf-viewer.png";
    public const string M3PageDnD = "docs/proof/m3-page-dnd.mp4";

    public static IReadOnlyList<string> ExpectedRelativePaths { get; } =
    [
        M1ShellTabs,
        M2PdfViewer,
        M3PageDnD,
    ];

    public const string ChecklistDoc = "docs/INTERACTIVE_VERIFY.md";

    /// <summary>Windows one-command orchestrator (download/sideload/probe + checklist).</summary>
    public const string OrchestratorScript = "scripts/interactive-verify.ps1";

    public const string SamplesDirectory = "docs/proof/samples";
    public const string SamplePdf = "docs/proof/samples/sample.pdf";
    public const string SamplePng = "docs/proof/samples/sample.png";

    public static IReadOnlyList<string> SampleRelativePaths { get; } =
    [
        SamplePdf,
        SamplePng,
    ];
}
