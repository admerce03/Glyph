using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class ThirdPartyNoticesPolicyTests
{
    [Fact]
    public void Notices_file_exists_and_covers_native_redistributables()
    {
        ThirdPartyNoticesPolicy.ShippedInRepo.Should().BeTrue();
        ThirdPartyNoticesPolicy.RepoRelativePath.Should().Be("THIRD_PARTY_NOTICES.md");
        ThirdPartyNoticesPolicy.AboutButton.Should().Contain("Third-party");

        var root = FindRepoRoot();
        var path = Path.Combine(root, ThirdPartyNoticesPolicy.RepoRelativePath);
        File.Exists(path).Should().BeTrue(path);
        var text = File.ReadAllText(path);
        text.Should().Contain("PDFium");
        text.Should().Contain("Magick.NET");
        text.Should().Contain("ImageMagick");
        text.Should().Contain("PdfPig");
        text.Should().Contain("Apache");
        text.Should().NotContain("CommunityToolkit.Mvvm");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Glyph.sln"))
                || File.Exists(Path.Combine(dir.FullName, ThirdPartyNoticesPolicy.RepoRelativePath)))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found from " + AppContext.BaseDirectory);
    }
}
