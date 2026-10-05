using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.Pdf;
using Glyph.Core.Signatures;

namespace Glyph.Core.Tests;

public class PackagingDeferredPolicyTests
{
    [Fact]
    public void Associations_and_updates_wait_on_verified_msix()
    {
        PackagingDeferredPolicy.MsixScaffoldShipped.Should().BeTrue();
        PackagingDeferredPolicy.MsixPackageCiProduced.Should().BeTrue();
        PackagingDeferredPolicy.MsixPackageCiTestSigned.Should().BeTrue();
        PackagingDeferredPolicy.NativeFileAssociationsShipped.Should().BeFalse();
        PackagingDeferredPolicy.InAppUpdateCheckShipped.Should().BeFalse();
        PackagingDeferredPolicy.Adr.Should().Be("ADR-012");
        PackagingDeferredPolicy.PublishScript.Should().Contain("publish-msix");
        PackagingDeferredPolicy.ManifestPath.Should().Contain("Package.appxmanifest");
        PackagingDeferredPolicy.Reason.Should().Contain("Test-signed");
    }
}

public class PdfOptimizeDeferredPolicyTests
{
    [Fact]
    public void Subset_and_linearize_unavailable()
    {
        PdfOptimizeDeferredPolicy.FontSubsettingSupported.Should().BeFalse();
        PdfOptimizeDeferredPolicy.LinearizeFastWebViewSupported.Should().BeFalse();
        PdfOptimizeDeferredPolicy.Adr.Should().Be("ADR-016");
    }
}

public class PdfPasswordWriteBlockedPolicyTests
{
    [Fact]
    public void Write_encrypt_blocked()
    {
        PdfPasswordWriteBlockedPolicy.CreatePasswordProtectedSupported.Should().BeFalse();
        PdfPasswordWriteBlockedPolicy.SetOpenPasswordSupported.Should().BeFalse();
        PdfPasswordWriteBlockedPolicy.Adr.Should().Be("ADR-015");
        PdfPasswordWriteBlockedPolicy.Reason.Should().Contain("ADR-015");
    }
}

public class SignatureCloudSyncDeferredTests
{
    [Fact]
    public void Cloud_sync_explicitly_later()
    {
        SignatureCloudSyncDeferred.ApplicationSpecificCloudSyncSupported.Should().BeFalse();
        SignatureCloudSyncDeferred.Reason.Should().Contain("later");
    }
}
