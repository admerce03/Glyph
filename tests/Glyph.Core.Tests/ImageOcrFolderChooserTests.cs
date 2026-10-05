using FluentAssertions;
using Glyph.Core.Ocr;

namespace Glyph.Core.Tests;

public class ImageOcrFolderChooserTests
{
    [Fact]
    public void Folder_offer_and_labels()
    {
        ImageOcrFolderChooser.ShouldOfferFolder(1).Should().BeFalse();
        ImageOcrFolderChooser.ShouldOfferFolder(4).Should().BeTrue();
        ImageOcrFolderChooser.SecondaryButton(4).Should().Be("Folder (4)");
        ImageOcrFolderChooser.Prompt(4).Should().Contain("4 images");
    }
}
