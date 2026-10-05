using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageDialogTitlesTests
{
    [Fact]
    public void Titles_are_stable()
    {
        ImageDialogTitles.ResizeImage.Should().Contain("Resize");
        ImageDialogTitles.ColorAdjustments.Should().Contain("Color");
        ImageDialogTitles.FolderOcrResults.Should().Contain("OCR");
        ImageDialogTitles.OcrEntities.Should().Contain("entities");
        ImageDialogTitles.DrawMarkup.Should().Contain("markup");
    }
}
