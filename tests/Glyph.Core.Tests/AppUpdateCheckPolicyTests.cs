using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class AppUpdateCheckPolicyTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3-beta", "1.2.3")]
    [InlineData("0.1.0", "0.1.0")]
    public void NormalizeVersion_strips_prefix_and_prerelease(string input, string expected)
    {
        AppUpdateCheckPolicy.NormalizeVersion(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("0.1.0", "0.2.0", -1)]
    [InlineData("0.2.0", "0.1.0", 1)]
    [InlineData("0.1.0", "0.1.0", 0)]
    [InlineData("v0.1.0", "0.1.0", 0)]
    public void CompareSemVer_orders_versions(string a, string b, int expectedSign)
    {
        var cmp = AppUpdateCheckPolicy.CompareSemVer(a, b);
        Math.Sign(cmp).Should().Be(expectedSign);
    }

    [Fact]
    public void FormatWithLatest_detects_newer_release()
    {
        AppUpdateCheckPolicy.FormatWithLatest("9.9.9").Should().Contain("newer");
        AppUpdateCheckPolicy.FormatWithLatest(AppUpdateCheckPolicy.ShippedVersion)
            .Should().Contain("latest");
    }

    [Fact]
    public void ShippedVersion_matches_package_manifest_identity()
    {
        AppUpdateCheckPolicy.ShippedVersion.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
        AppUpdateCheckPolicy.ReleasesUrl.Should().Contain("releases");

        var root = FindRepoRoot();
        var manifestPath = Path.Combine(root, PackageFileAssociationDeclaration.ManifestRelativePath);
        File.Exists(manifestPath).Should().BeTrue(manifestPath);
        var xml = File.ReadAllText(manifestPath);
        // Identity Version is four-part (0.1.0.0); shipped marketing version is three-part.
        xml.Should().Contain($"Version=\"{AppUpdateCheckPolicy.ShippedVersion}.0\"");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Glyph.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found from " + AppContext.BaseDirectory);
    }
}
