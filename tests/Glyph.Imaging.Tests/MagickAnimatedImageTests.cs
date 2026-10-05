using FluentAssertions;
using Glyph.Imaging.Magick;
using ImageMagick;

namespace Glyph.Imaging.Tests;

public class MagickAnimatedImageTests
{
    [Fact]
    public async Task OpenAsync_loads_animated_gif_frames()
    {
        var path = await WriteAnimatedGifAsync(frameCount: 3, delayCs: 12);
        try
        {
            var decoder = new MagickImageDecoder();
            await using var document = await decoder.OpenAsync(path);

            document.FrameCount.Should().Be(3);
            document.CurrentFrameIndex.Should().Be(0);
            document.PixelWidth.Should().Be(16);
            document.PixelHeight.Should().Be(12);
            document.GetFrameDelayMilliseconds(0).Should().Be(120);
            document.AnimationIterations.Should().Be(0);

            await document.SetCurrentFrameAsync(2);
            document.CurrentFrameIndex.Should().Be(2);

            var extracted = await document.ExtractFrameAsync(1);
            extracted.Width.Should().Be(16);
            extracted.Height.Should().Be(12);
            extracted.BgraPixels.Length.Should().Be(16 * 12 * 4);
            document.CurrentFrameIndex.Should().Be(2);

            var meta = await document.GetMetadataAsync();
            meta.Entries.Should().Contain(e => e.Group == "Animation" && e.Name == "Frames" && e.Value == "3");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task OpenAsync_still_image_reports_single_frame()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-still-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var image = new MagickImage(MagickColors.Orange, 20, 10))
            {
                image.Format = MagickFormat.Png;
                await image.WriteAsync(path);
            }

            var decoder = new MagickImageDecoder();
            await using var document = await decoder.OpenAsync(path);
            document.FrameCount.Should().Be(1);
            document.CurrentFrameIndex.Should().Be(0);
            document.AnimationIterations.Should().Be(1);
            document.GetFrameDelayMilliseconds(0).Should().Be(100);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SetCurrentFrameAsync_rejects_out_of_range()
    {
        var path = await WriteAnimatedGifAsync(frameCount: 2, delayCs: 5);
        try
        {
            var decoder = new MagickImageDecoder();
            await using var document = await decoder.OpenAsync(path);
            var act = async () => await document.SetCurrentFrameAsync(5);
            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static async Task<string> WriteAnimatedGifAsync(int frameCount, int delayCs)
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-anim-" + Guid.NewGuid().ToString("N") + ".gif");
        using var collection = new MagickImageCollection();
        for (var i = 0; i < frameCount; i++)
        {
            var color = i % 3 == 0
                ? MagickColors.Red
                : i % 3 == 1
                    ? MagickColors.Lime
                    : MagickColors.Blue;
            var frame = new MagickImage(color, 16, 12)
            {
                Format = MagickFormat.Gif,
                AnimationDelay = (uint)delayCs,
                AnimationTicksPerSecond = 100,
                AnimationIterations = 0,
            };
            collection.Add(frame);
        }

        await collection.WriteAsync(path);
        return path;
    }
}
