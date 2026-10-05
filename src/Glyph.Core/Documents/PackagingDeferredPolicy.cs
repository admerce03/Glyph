namespace Glyph.Core.Documents;

/// <summary>
/// Packaging items deferred on ADR-012 (F01-06/07).
/// Windows CI produces a test-signed <c>.msix</c> via <c>scripts/publish-msix.ps1 -TestSign</c>;
/// Explorer associations stay deferred until sideload default-app assignment is verified.
/// Manual update check (F55-04) is shipped separately via <see cref="AppUpdateCheckPolicy"/>.
/// </summary>
public static class PackagingDeferredPolicy
{
    /// <summary>Manifest + publish script + conditional MSIX csproj wiring are in-repo.</summary>
    public const bool MsixScaffoldShipped = true;

    /// <summary>Windows CI <c>GenerateAppxPackageOnBuild</c> emits <c>Glyph.App_*.msix</c>.</summary>
    public const bool MsixPackageCiProduced = true;

    /// <summary>
    /// CI ephemeral self-signed cert (<c>CN=Glyph</c>) signs the package and exports
    /// <c>Glyph.CI.TestSign.cer</c> for Trusted People install before Developer Mode sideload.
    /// </summary>
    public const bool MsixPackageCiTestSigned = true;

    /// <summary>
    /// <c>scripts/install-msix-test.ps1</c> trusts the CI test cert and runs <c>Add-AppxPackage</c>.
    /// Associations stay deferred until that install is verified on a Windows machine.
    /// </summary>
    public const bool MsixSideloadHelperShipped = true;

    /// <summary>
    /// Sideload helper probes installed <c>Get-AppxPackageManifest</c> file types after install
    /// (and via <c>-VerifyOnly</c>). Explorer default-app assignment is still manual.
    /// </summary>
    public const bool MsixSideloadAssociationProbeShipped = true;

    public const bool NativeFileAssociationsShipped = false;
    public const bool ConfigurableDefaultAssociationsShipped = false;

    /// <summary>Manual Check for updates (F55-04) via <see cref="AppUpdateCheckPolicy"/>.</summary>
    public const bool InAppUpdateCheckShipped = true;

    public const string Adr = "ADR-012";

    public const string Reason =
        "Test-signed .msix + sideload helper/association probe shipped; Explorer defaults wait on verified sideload.";

    public const string PublishScript = "scripts/publish-msix.ps1";
    public const string InstallScript = "scripts/install-msix-test.ps1";
    public const string ManifestPath = "src/Glyph.App/Package.appxmanifest";

    /// <summary>Operator guide for MSIX / sideload / Store signing posture.</summary>
    public const string PackagingDocsPath = "docs/PACKAGING.md";
}
