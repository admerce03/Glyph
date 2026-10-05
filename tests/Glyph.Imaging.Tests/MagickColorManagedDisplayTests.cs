using FluentAssertions;
using Glyph.Imaging.Abstractions;
using Glyph.Imaging.Magick;
using ImageMagick;

namespace Glyph.Imaging.Tests;

public class MagickColorManagedDisplayTests
{
    [Fact]
    public async Task GetPixelsAsync_color_managed_converts_adobe_rgb_without_mutating_file_profile()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-cm-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var image = new MagickImage(new MagickColor("#C83232"), 24, 16))
            {
                image.SetProfile(ColorProfiles.AdobeRGB1998);
                image.Format = MagickFormat.Png;
                await image.WriteAsync(path);
            }

            var decoder = new MagickImageDecoder();
            await using var document = await decoder.OpenAsync(path);
            document.ColorManagedDisplay.Should().BeTrue();
            (await document.GetMetadataAsync()).HasIccProfile.Should().BeTrue();

            var managed = await document.GetPixelsAsync();
            document.ColorManagedDisplay = false;
            var raw = await document.GetPixelsAsync();

            // Display transform should not strip the stored profile.
            (await document.GetMetadataAsync()).HasIccProfile.Should().BeTrue();
            managed.BgraPixels.Length.Should().Be(raw.BgraPixels.Length);

            // Soft-proof path also leaves stored profile intact.
            document.ColorManagedDisplay = true;
            document.SoftProofProfile = ImageColorProfileKind.AdobeRgb;
            document.DisplayRenderingIntent = ImageRenderingIntent.Relative;
            var soft = await document.GetPixelsAsync();
            soft.BgraPixels.Length.Should().Be(managed.BgraPixels.Length);
            (await document.GetMetadataAsync()).HasIccProfile.Should().BeTrue();
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
