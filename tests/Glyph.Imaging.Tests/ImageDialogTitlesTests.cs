using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Xunit;

namespace Glyph.Imaging.Tests;

public class ImageDialogTitlesTests
{
    [Fact]
    public void Titles_are_stable()
    {
        ImageDialogTitles.ResizeImage.Should().Contain("Resize");
        ImageDialogTitles.ColorAdjustments.Should().Contain("Color");
        ImageDialogTitles.FolderOcrResults.Should().Contain("OCR");
        ImageDialogTitles.DrawMarkup.Should().Contain("markup");
    }
}
