namespace Glyph.Core.Documents;

/// <summary>
/// Packaging / update-channel items deferred on ADR-012 (F01-06/07, F55-04).
/// MSIX scaffold exists (<c>Package.appxmanifest</c> + <c>scripts/publish-msix.ps1</c>);
/// associations and update-check stay deferred until a signed/sideload install is verified.
/// </summary>
public static class PackagingDeferredPolicy
{
    /// <summary>Manifest + publish script + conditional MSIX csproj wiring are in-repo.</summary>
    public const bool MsixScaffoldShipped = true;

    public const bool NativeFileAssociationsShipped = false;
    public const bool ConfigurableDefaultAssociationsShipped = false;
    public const bool InAppUpdateCheckShipped = false;

    public const string Adr = "ADR-012";

    public const string Reason =
        "MSIX scaffold shipped; durable file associations and update channel wait on verified installer/sideload.";

    public const string PublishScript = "scripts/publish-msix.ps1";
    public const string ManifestPath = "src/Glyph.App/Package.appxmanifest";
}
