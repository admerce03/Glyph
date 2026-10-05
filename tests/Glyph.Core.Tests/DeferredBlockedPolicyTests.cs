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
        PackagingDeferredPolicy.MsixSideloadHelperShipped.Should().BeTrue();
        PackagingDeferredPolicy.MsixSideloadAssociationProbeShipped.Should().BeTrue();
        PackagingDeferredPolicy.MsixSideloadCiAssociationProbe.Should().BeTrue();
        PackagingDeferredPolicy.NativeFileAssociationsShipped.Should().BeFalse();
        PackagingDeferredPolicy.InAppUpdateCheckShipped.Should().BeTrue();
        PackagingDeferredPolicy.Adr.Should().Be("ADR-012");
        PackagingDeferredPolicy.PublishScript.Should().Contain("publish-msix");
        PackagingDeferredPolicy.InstallScript.Should().Contain("install-msix-test");
        PackagingDeferredPolicy.ManifestPath.Should().Contain("Package.appxmanifest");
        PackagingDeferredPolicy.PackagingDocsPath.Should().Be("docs/PACKAGING.md");
        PackagingDeferredPolicy.Reason.Should().Contain("CI sideload");
        PackagingDeferredPolicy.Reason.Should().Contain("association probe");
    }

    [Fact]
    public void Ci_workflow_runs_msix_sideload_association_probe()
    {
        var root = FindRepoRoot();
        var ciPath = Path.Combine(root, ".github", "workflows", "ci.yml");
        File.Exists(ciPath).Should().BeTrue(ciPath);
        var yaml = File.ReadAllText(ciPath);
        yaml.Should().Contain("install-msix-test.ps1");
        yaml.Should().Contain("-Force");
        yaml.Should().Contain("-ProbeUserDefaults");
        yaml.Should().Contain("AllowDevelopmentWithoutDevLicense");
    }

    [Fact]
    public void Packaging_docs_exist_at_policy_path()
    {
        var root = FindRepoRoot();
        var docsPath = Path.Combine(root, PackagingDeferredPolicy.PackagingDocsPath);
        File.Exists(docsPath).Should().BeTrue(docsPath);
        var text = File.ReadAllText(docsPath);
        text.Should().Contain("publish-msix");
        text.Should().Contain("install-msix-test");
        text.Should().Contain("ADR-012");
        text.Should().Contain("Store");
    }

    [Fact]
    public void Package_manifest_declares_pdf_and_image_associations()
    {
        var root = FindRepoRoot();
        var manifestPath = Path.Combine(root, PackageFileAssociationDeclaration.ManifestRelativePath);
        File.Exists(manifestPath).Should().BeTrue(manifestPath);
        var xml = File.ReadAllText(manifestPath);
        PackageFileAssociationDeclaration.ManifestDeclaresExpectedAssociations(xml).Should().BeTrue();
        PackageFileAssociationDeclaration.ParseDeclaredExtensions(xml)
            .Should().Contain(PackageFileAssociationDeclaration.ExpectedExtensions);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Glyph.sln"))
                || File.Exists(Path.Combine(dir.FullName, PackagingDeferredPolicy.ManifestPath)))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found from " + AppContext.BaseDirectory);
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
