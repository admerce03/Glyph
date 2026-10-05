using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageDragSemanticsTests
{
    [Fact]
    public void CanDragFile_requires_existing_path()
    {
        ImageDragSemantics.CanDragFile(null).Should().BeFalse();
        ImageDragSemantics.CanDragFile("").Should().BeFalse();
        ImageDragSemantics.CanDragFile(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid() + ".png"))
            .Should().BeFalse();

        var path = Path.Combine(Path.GetTempPath(), "glyph-drag-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            File.WriteAllBytes(path, [1, 2, 3]);
            ImageDragSemantics.CanDragFile(path).Should().BeTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
