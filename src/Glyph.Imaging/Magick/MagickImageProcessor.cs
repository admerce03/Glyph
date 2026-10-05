using Glyph.Imaging.Abstractions;
using ImageMagick;

namespace Glyph.Imaging.Magick;

public sealed class MagickImageProcessor : IImageProcessor
{
    public Task CropAsync(IImageDocument document, ImageRect pixels, CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        if (pixels.Width <= 0 || pixels.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixels));
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                magick.Native.Crop(new MagickGeometry(pixels.X, pixels.Y, (uint)pixels.Width, (uint)pixels.Height));
                magick.Native.ResetPage();
            },
            cancellationToken);
    }

    public Task ResizeAsync(IImageDocument document, int width, int height, CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                magick.Native.Resize((uint)width, (uint)height);
            },
            cancellationToken);
    }

    public Task RotateAsync(IImageDocument document, int degreesClockwise, CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        if (degreesClockwise % 90 != 0)
        {
            throw new ArgumentException("Rotation must be a multiple of 90 degrees.", nameof(degreesClockwise));
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalized = ((degreesClockwise % 360) + 360) % 360;
                if (normalized == 0)
                {
                    return;
                }

                magick.Native.Rotate(normalized);
            },
            cancellationToken);
    }

    public Task FlipHorizontalAsync(IImageDocument document, CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                magick.Native.Flop();
            },
            cancellationToken);
    }

    public Task FlipVerticalAsync(IImageDocument document, CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                magick.Native.Flip();
            },
            cancellationToken);
    }

    public Task AdjustAsync(IImageDocument document, ImageAdjustments adjustments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adjustments);
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (adjustments.AutoLevels)
                {
                    magick.Native.AutoLevel();
                }

                if (Math.Abs(adjustments.Brightness) > 0.0001)
                {
                    // Magick brightness-contrast: brightness percent roughly -100..100.
                    magick.Native.BrightnessContrast(new Percentage(adjustments.Brightness), new Percentage(0));
                }

                if (Math.Abs(adjustments.Contrast) > 0.0001)
                {
                    magick.Native.BrightnessContrast(new Percentage(0), new Percentage(adjustments.Contrast));
                }

                if (Math.Abs(adjustments.Saturation) > 0.0001)
                {
                    magick.Native.Modulate(new Percentage(100), new Percentage(100 + adjustments.Saturation), new Percentage(100));
                }

                if (adjustments.Sharpness > 0.0001)
                {
                    // Radius/sigma scaled from a 0–100 UI slider.
                    var sigma = Math.Clamp(adjustments.Sharpness / 25.0, 0.1, 4.0);
                    magick.Native.Sharpen(radius: 0, sigma: sigma);
                }

                if (adjustments.Sepia)
                {
                    magick.Native.SepiaTone(new Percentage(80));
                }
            },
            cancellationToken);
    }

    public Task RemoveGpsMetadataAsync(IImageDocument document, CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var exif = magick.Native.GetExifProfile();
                if (exif is null)
                {
                    return;
                }

                var gpsTags = exif.Values
                    .Where(v => v.Tag.ToString().StartsWith("GPS", StringComparison.Ordinal))
                    .Select(v => v.Tag)
                    .Distinct()
                    .ToList();

                if (gpsTags.Count == 0)
                {
                    return;
                }

                foreach (var tag in gpsTags)
                {
                    exif.RemoveValue(tag);
                }

                magick.Native.SetProfile(exif);
            },
            cancellationToken);
    }

    public Task NormalizeOrientationAsync(IImageDocument document, CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                magick.Native.AutoOrient();
                MagickImageDecoder.ClearExifOrientation(magick.Native);
            },
            cancellationToken);
    }

    private static MagickImageDocument RequireMagick(IImageDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document is not MagickImageDocument magick)
        {
            throw new ArgumentException("Document must be opened by MagickImageDecoder.", nameof(document));
        }

        return magick;
    }
}
