using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.Pdf;
using Glyph.Core.Text;

namespace Glyph.Core.Tests;

public class PdfRegionCopyAndOcrFallbackTests
{
    [Theory]
    [InlineData(0, 10, 10, true)]
    [InlineData(-1, 10, 10, false)]
    [InlineData(0, 3, 10, false)]
    [InlineData(0, 10, 3, false)]
    public void HasValidRegion(int page, double w, double h, bool expected)
    {
        PdfRegionCopyPolicy.HasValidRegion(page, w, h).Should().Be(expected);
    }

    [Fact]
    public void TextDrag_and_OCR_fallback()
    {
        PdfTextDragPolicy.CanStartTextDrag("hi").Should().BeTrue();
        PdfTextDragPolicy.CanStartTextDrag("  ").Should().BeFalse();
        FindOcrFallbackPolicy.ShouldOfferOcr("OCR required.").Should().BeTrue();
        FindOcrFallbackPolicy.ShouldOfferOcr("No matches.").Should().BeFalse();
        FindOcrFallbackPolicy.PrimaryButton.Should().Contain("OCR");
    }
}

public class ViewerChromePolicyTests
{
    [Fact]
    public void Toolbar_names_and_title_bar()
    {
        ViewerToolbarAutomationNames.RequiredNames.Should().Contain("Magnifier loupe");
        SystemTitleBarPolicy.UsesSystemTitleBar.Should().BeTrue();
        PointerInputPolicy.IsPrimaryButton(true).Should().BeTrue();
    }

    [Fact]
    public void PdfDocumentView_sets_required_automation_names()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "src/Glyph.App/Views/PdfDocumentView.cs"));
        foreach (var name in ViewerToolbarAutomationNames.RequiredNames)
        {
            src.Should().Contain($"\"{name}\"", because: name);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src/Glyph.App/Views/PdfDocumentView.cs")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found");
    }
}

public class PdfRenderCapabilitiesTests
{
    [Fact]
    public void Declares_vector_image_font_transparency()
    {
        PdfRenderCapabilities.DeclaredCapabilities.Should().HaveCount(4);
        PdfRenderCapabilities.VectorContent.Should().BeTrue();
        PdfRenderCapabilities.Transparency.Should().BeTrue();
    }
}
