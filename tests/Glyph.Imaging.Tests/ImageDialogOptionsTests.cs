using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageDialogOptionsTests
{
    [Fact]
    public void Option_lists_are_stable()
    {
        ImageDialogOptions.CropAspectRatios.Should().Contain("1:1");
        ImageDialogOptions.SelectionTools.Should().Contain("Lasso");
        ImageDialogOptions.ExportFormats.Should().Contain("PNG");
        ImageDialogOptions.MarkupColors.Should().Contain("Red");
    }
}
