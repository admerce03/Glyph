namespace Glyph.Core.Documents;

/// <summary>
/// Packaging / update-channel items deferred on ADR-012 (F01-06/07, F55-04).
/// Windows CI produces a test-signed <c>.msix</c> via <c>scripts/publish-msix.ps1 -TestSign</c>;
/// associations and update-check stay deferred until a signed/sideload install is verified.
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

    public const bool NativeFileAssociationsShipped = false;
    public const bool ConfigurableDefaultAssociationsShipped = false;
    public const bool InAppUpdateCheckShipped = false;

    public const string Adr = "ADR-012";

    public const string Reason =
        "Test-signed .msix builds on CI; durable file associations and update channel wait on verified sideload.";

    public const string PublishScript = "scripts/publish-msix.ps1";
    public const string ManifestPath = "src/Glyph.App/Package.appxmanifest";
}
