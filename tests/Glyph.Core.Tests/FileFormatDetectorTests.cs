using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.IO;

namespace Glyph.Core.Tests;

public class FileFormatDetectorTests
{
    [Theory]
    [InlineData("report.PDF", DocumentKind.Pdf)]
    [InlineData(@"C:\temp\scan.pdf", DocumentKind.Pdf)]
    [InlineData("photo.jpeg", DocumentKind.Image)]
    [InlineData("diagram.PNG", DocumentKind.Image)]
    [InlineData("shot.avif", DocumentKind.Image)]
    [InlineData("phone.HEIC", DocumentKind.Image)]
    [InlineData("scan.jp2", DocumentKind.Image)]
    [InlineData("archive.zip", DocumentKind.Unknown)]
    public void DetectKind_uses_extension(string path, DocumentKind expected)
    {
        FileFormatDetector.DetectKind(path).Should().Be(expected);
    }

    [Fact]
    public void IsSupported_returns_false_for_unknown_types()
    {
        FileFormatDetector.IsSupported("notes.docx").Should().BeFalse();
    }

    [Fact]
    public void SupportedExtensions_matches_DetectKind()
    {
        FileFormatDetector.SupportedExtensions.Should().NotBeEmpty();
        FileFormatDetector.SupportedExtensions.Should().OnlyHaveUniqueItems();
        FileFormatDetector.SupportedExtensions.Should().Contain(".pdf");
        FileFormatDetector.SupportedExtensions.Should().Contain(".png");
        FileFormatDetector.SupportedExtensions.Should().Contain(".j2k");

        foreach (var extension in FileFormatDetector.SupportedExtensions)
        {
            FileFormatDetector.IsSupported("file" + extension).Should().BeTrue(extension);
            FileFormatDetector.DetectKind("file" + extension).Should().NotBe(DocumentKind.Unknown);
        }
    }

    [Fact]
    public void FilterSupportedPaths_keeps_openable_files_only()
    {
        var filtered = FileFormatDetector.FilterSupportedPaths(
        [
            @"C:\docs\a.pdf",
            @"C:\docs\notes.docx",
            "",
            @"D:\photos\b.JPEG",
            "   ",
            @"E:\raw.zip",
        ]);

        filtered.Should().Equal(@"C:\docs\a.pdf", @"D:\photos\b.JPEG");
    }
}
