using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Xunit;

namespace Glyph.Imaging.Tests;

public class ImageDialogPlaceholdersTests
{
    [Fact]
    public void Placeholders_are_stable()
    {
        ImageDialogPlaceholders.CropXyWh.Should().Contain("Crop");
    }
}
