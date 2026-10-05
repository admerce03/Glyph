using Glyph.Imaging.Abstractions;
using ImageMagick;
using ImageMagick.Drawing;

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

    public Task ResizeAsync(
        IImageDocument document,
        int width,
        int height,
        ImageResizeOptions? options = null,
        CancellationToken cancellationToken = default)
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
                if (options?.Filter is { } filter and not ImageResizeFilter.Auto)
                {
                    magick.Native.FilterType = filter switch
                    {
                        ImageResizeFilter.NearestNeighbor => FilterType.Point,
                        ImageResizeFilter.Bilinear => FilterType.Triangle,
                        ImageResizeFilter.Bicubic => FilterType.Cubic,
                        _ => magick.Native.FilterType,
                    };
                }

                magick.Native.Resize((uint)width, (uint)height);
                if (options?.DensityDpi is double dpi && dpi > 0)
                {
                    magick.Native.Density = new Density(dpi, dpi, DensityUnit.PixelsPerInch);
                }
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

                // Temperature: warm (+) boosts red/reduces blue; Tint: green (−) / magenta (+).
                if (Math.Abs(adjustments.Temperature) > 0.0001 || Math.Abs(adjustments.Tint) > 0.0001)
                {
                    var temp = Math.Clamp(adjustments.Temperature, -100, 100) / 100.0;
                    var tint = Math.Clamp(adjustments.Tint, -100, 100) / 100.0;
                    var rGain = 1.0 + (0.18 * temp);
                    var gGain = 1.0 + (0.12 * tint);
                    var bGain = 1.0 - (0.18 * temp);
                    magick.Native.ColorMatrix(new MagickColorMatrix(
                        5,
                        rGain, 0, 0, 0, 0,
                        0, gGain, 0, 0, 0,
                        0, 0, bGain, 0, 0,
                        0, 0, 0, 1, 0,
                        0, 0, 0, 0, 1));
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

    public Task ClearRectAsync(
        IImageDocument document,
        ImageRect pixels,
        bool transparent = true,
        CancellationToken cancellationToken = default)
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
                var x = Math.Clamp(pixels.X, 0, Math.Max(0, (int)magick.Native.Width - 1));
                var y = Math.Clamp(pixels.Y, 0, Math.Max(0, (int)magick.Native.Height - 1));
                var right = Math.Clamp(pixels.X + pixels.Width, x + 1, (int)magick.Native.Width);
                var bottom = Math.Clamp(pixels.Y + pixels.Height, y + 1, (int)magick.Native.Height);
                if (transparent)
                {
                    magick.Native.Alpha(AlphaOption.Set);
                }

                var fill = transparent ? MagickColors.Transparent : MagickColors.White;
                new Drawables()
                    .FillColor(fill)
                    .Rectangle(x, y, right - 1, bottom - 1)
                    .Draw(magick.Native);
            },
            cancellationToken);
    }

    public Task<ImagePixelBuffer> ExtractRectAsync(
        IImageDocument document,
        ImageRect pixels,
        CancellationToken cancellationToken = default)
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
                using var clone = magick.Native.Clone();
                clone.Crop(new MagickGeometry(pixels.X, pixels.Y, (uint)pixels.Width, (uint)pixels.Height));
                clone.ResetPage();
                clone.Depth = 8;
                clone.ColorType = ColorType.TrueColorAlpha;
                var bgra = clone.ToByteArray(MagickFormat.Bgra);
                return new ImagePixelBuffer(checked((int)clone.Width), checked((int)clone.Height), bgra);
            },
            cancellationToken);
    }

    public Task PasteRectAsync(
        IImageDocument document,
        ImagePixelBuffer source,
        int destinationX,
        int destinationY,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Width <= 0 || source.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var settings = new PixelReadSettings((uint)source.Width, (uint)source.Height, StorageType.Char, "BGRA");
                using var overlay = new MagickImage();
                overlay.ReadPixels(source.BgraPixels, settings);
                var x = Math.Clamp(destinationX, 0, Math.Max(0, (int)magick.Native.Width - 1));
                var y = Math.Clamp(destinationY, 0, Math.Max(0, (int)magick.Native.Height - 1));
                magick.Native.Composite(overlay, x, y, CompositeOperator.Over);
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
