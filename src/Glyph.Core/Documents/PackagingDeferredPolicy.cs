namespace Glyph.Core.Documents;

/// <summary>
/// Packaging items deferred on ADR-012 (F01-06/07).
/// Windows CI produces a test-signed <c>.msix</c> via <c>scripts/publish-msix.ps1 -TestSign</c>,
/// sideloads it, and probes installed manifest associations; Explorer default-app assignment
/// (UserChoice) stays deferred until interactive verify.
/// Manual update check (F55-04) is shipped separately via <see cref="AppUpdateCheckPolicy"/>.
/// </summary>
public static class PackagingDeferredPolicy
{
    /// <summary>Manifest + publish script + conditional MSIX csproj wiring are in-repo.</summary>
    public const bool MsixScaffoldShipped = true;

    /// <summary>Windows CI <c>GenerateAppxPackageOnBuild</c> emits <c>Glyph.App_*.msix</c> (win-x64).</summary>
    public const bool MsixPackageCiProduced = true;

    /// <summary>
    /// Windows CI also publishes a test-signed <c>win-arm64</c> package (artifact
    /// <c>glyph-msix-layout-arm64</c>). Sideload/association probe runs on the x64 runner only.
    /// </summary>
    public const bool MsixPackageCiArm64Produced = true;

    /// <summary>
    /// CI ephemeral self-signed cert (<c>CN=Glyph</c>) signs the package and exports
    /// <c>Glyph.CI.TestSign.cer</c> for Trusted People install before Developer Mode sideload.
    /// </summary>
    public const bool MsixPackageCiTestSigned = true;

    /// <summary>
    /// <c>scripts/install-msix-test.ps1</c> trusts the CI test cert and runs <c>Add-AppxPackage</c>.
    /// </summary>
    public const bool MsixSideloadHelperShipped = true;

    /// <summary>
    /// Sideload helper probes installed <c>Get-AppxPackageManifest</c> file types after install
    /// (and via <c>-VerifyOnly</c>). Explorer default-app assignment is still manual.
    /// </summary>
    public const bool MsixSideloadAssociationProbeShipped = true;

    /// <summary>
    /// Windows CI enables AppModelUnlock sideloading, runs <c>install-msix-test.ps1 -Force -ProbeUserDefaults</c>,
    /// and fails the job if installed <c>uap:FileType</c> associations are incomplete.
    /// </summary>
    public const bool MsixSideloadCiAssociationProbe = true;

    public const bool NativeFileAssociationsShipped = false;
    public const bool ConfigurableDefaultAssociationsShipped = false;

    /// <summary>Manual Check for updates (F55-04) via <see cref="AppUpdateCheckPolicy"/>.</summary>
    public const bool InAppUpdateCheckShipped = true;

    public const string Adr = "ADR-012";

    public const string Reason =
        "Test-signed .msix + CI sideload/association probe shipped; Explorer UserChoice defaults wait on interactive verify.";

    public const string PublishScript = "scripts/publish-msix.ps1";
    public const string InstallScript = "scripts/install-msix-test.ps1";
    public const string ManifestPath = "src/Glyph.App/Package.appxmanifest";

    /// <summary>Operator guide for MSIX / sideload / Store signing posture.</summary>
    public const string PackagingDocsPath = "docs/PACKAGING.md";
}
