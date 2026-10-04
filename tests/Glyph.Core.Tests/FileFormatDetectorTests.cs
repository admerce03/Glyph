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
}
