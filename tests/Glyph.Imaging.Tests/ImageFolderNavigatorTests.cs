using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageFolderNavigatorTests
{
    [Fact]
    public void ListSiblings_orders_image_files_and_skips_non_images()
    {
        var dir = CreateTempDir();
        try
        {
            Touch(dir, "b.png");
            Touch(dir, "a.jpg");
            Touch(dir, "notes.txt");
            Touch(dir, "c.WEBP");

            var current = Path.Combine(dir, "a.jpg");
            var siblings = ImageFolderNavigator.ListSiblings(current);
            siblings.Select(Path.GetFileName).Should().Equal("a.jpg", "b.png", "c.WEBP");
            ImageFolderNavigator.IndexOf(siblings, current).Should().Be(0);
            ImageFolderNavigator.Previous(siblings, current).Should().BeNull();
            Path.GetFileName(ImageFolderNavigator.Next(siblings, current)!).Should().Be("b.png");

            var mid = Path.Combine(dir, "b.png");
            Path.GetFileName(ImageFolderNavigator.Previous(siblings, mid)!).Should().Be("a.jpg");
            Path.GetFileName(ImageFolderNavigator.Next(siblings, mid)!).Should().Be("c.WEBP");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void IsImagePath_recognizes_common_extensions()
    {
        ImageFolderNavigator.IsImagePath(@"C:\pics\photo.JPEG").Should().BeTrue();
        ImageFolderNavigator.IsImagePath(@"C:\pics\shot.avif").Should().BeTrue();
        ImageFolderNavigator.IsImagePath(@"C:\pics\phone.heic").Should().BeTrue();
        ImageFolderNavigator.IsImagePath(@"C:\pics\scan.jp2").Should().BeTrue();
        ImageFolderNavigator.IsImagePath(@"C:\pics\doc.pdf").Should().BeFalse();
        ImageFolderNavigator.IsImagePath("").Should().BeFalse();
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "glyph-nav-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Touch(string dir, string name)
        => File.WriteAllText(Path.Combine(dir, name), "x");
}
