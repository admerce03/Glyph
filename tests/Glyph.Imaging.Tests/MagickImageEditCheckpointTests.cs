using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Glyph.Imaging.Magick;
using ImageMagick;

namespace Glyph.Imaging.Tests;

public class MagickImageEditCheckpointTests
{
    [Fact]
    public async Task Capture_and_restore_undoes_crop()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-undo-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var image = new MagickImage(MagickColors.Red, 80, 60))
            {
                await image.WriteAsync(path);
            }

            var decoder = new MagickImageDecoder();
            await using var document = await decoder.OpenAsync(path);
            document.PixelWidth.Should().Be(80);
            document.PixelHeight.Should().Be(60);

            var checkpoint = document.CaptureCheckpoint();
            var processor = new MagickImageProcessor();
            await processor.CropAsync(document, new ImageRect(10, 10, 40, 30));
            document.PixelWidth.Should().Be(40);
            document.PixelHeight.Should().Be(30);

            document.RestoreCheckpoint(checkpoint);
            document.PixelWidth.Should().Be(80);
            document.PixelHeight.Should().Be(60);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
