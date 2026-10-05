using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Xunit;

namespace Glyph.Imaging.Tests;

public class ImageDescriptiveMetadataSummaryTests
{
    private static ImageMetadataInfo Blank(
        string? title = null,
        string? description = null,
        string? keywords = null,
        string? copyright = null,
        int? rating = null,
        bool hasIptc = false,
        bool hasXmp = false) =>
        new(
            1, 1, null, "PNG", null, null, null, null, false, null,
            null, null, null, null, null, null, null, null, null, null, null,
            title, description, keywords, copyright, rating, hasIptc, hasXmp, []);

    [Fact]
    public void HasAny_false_when_empty()
    {
        ImageDescriptiveMetadataSummary.HasAny(Blank()).Should().BeFalse();
        ImageDescriptiveMetadataSummary.Format(Blank()).Should().BeEmpty();
    }

    [Fact]
    public void Format_title_only()
    {
        var info = Blank(title: "Hello");
        ImageDescriptiveMetadataSummary.HasAny(info).Should().BeTrue();
        ImageDescriptiveMetadataSummary.Format(info).Should().Be("Title: Hello");
    }

    [Fact]
    public void Format_includes_lens_model()
    {
        var info = Blank(title: "T") with { LensModel = "EF 50mm" };
        ImageDescriptiveMetadataSummary.Format(info).Should().Contain("Lens: EF 50mm");
        ImageDescriptiveMetadataSummary.AutomationNameFromDescription(Blank(description: "Alt"))
            .Should().Be("Alt");
    }
}
