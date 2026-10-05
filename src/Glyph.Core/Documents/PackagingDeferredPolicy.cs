namespace Glyph.Core.Documents;

/// <summary>
/// Packaging / update-channel items deferred on ADR-012 (F01-06/07, F55-04).
/// </summary>
public static class PackagingDeferredPolicy
{
    public const bool NativeFileAssociationsShipped = false;
    public const bool ConfigurableDefaultAssociationsShipped = false;
    public const bool InAppUpdateCheckShipped = false;

    public const string Adr = "ADR-012";

    public const string Reason =
        "Wait for MSIX/installer packaging before file associations and update channel.";
}
