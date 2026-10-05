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

                if (Math.Abs(adjustments.Shadows) > 0.0001 || Math.Abs(adjustments.Highlights) > 0.0001)
                {
                    ApplyShadowsHighlights(
                        magick.Native,
                        Math.Clamp(adjustments.Shadows, -100, 100),
                        Math.Clamp(adjustments.Highlights, -100, 100));
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
        ImageSelectionKind kind = ImageSelectionKind.Rectangle,
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
                ClearRegionCore(magick.Native, pixels, transparent, kind);
            },
            cancellationToken);
    }

    public Task<ImagePixelBuffer> ExtractRectAsync(
        IImageDocument document,
        ImageRect pixels,
        ImageSelectionKind kind = ImageSelectionKind.Rectangle,
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
                return ExtractRegionCore(magick.Native, pixels, kind);
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
                PasteRectCore(magick.Native, source, destinationX, destinationY);
            },
            cancellationToken);
    }

    public Task MoveRectAsync(
        IImageDocument document,
        ImageRect source,
        int destinationX,
        int destinationY,
        ImageSelectionKind kind = ImageSelectionKind.Rectangle,
        CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        if (source.Width <= 0 || source.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (destinationX == source.X && destinationY == source.Y)
                {
                    return;
                }

                var buffer = ExtractRegionCore(magick.Native, source, kind);
                ClearRegionCore(magick.Native, source, transparent: true, kind);
                PasteRectCore(magick.Native, buffer, destinationX, destinationY);
            },
            cancellationToken);
    }

    private static ImagePixelBuffer ExtractRegionCore(MagickImage image, ImageRect pixels, ImageSelectionKind kind)
    {
        using var clone = (MagickImage)image.Clone();
        clone.Crop(new MagickGeometry(pixels.X, pixels.Y, (uint)pixels.Width, (uint)pixels.Height));
        clone.ResetPage();
        clone.Depth = 8;
        clone.ColorType = ColorType.TrueColorAlpha;
        if (kind == ImageSelectionKind.Ellipse)
        {
            ApplyEllipseAlphaMask(clone);
        }

        var bgra = clone.ToByteArray(MagickFormat.Bgra);
        return new ImagePixelBuffer(checked((int)clone.Width), checked((int)clone.Height), bgra);
    }

    private static void ClearRegionCore(
        MagickImage image,
        ImageRect pixels,
        bool transparent,
        ImageSelectionKind kind)
    {
        var x = Math.Clamp(pixels.X, 0, Math.Max(0, (int)image.Width - 1));
        var y = Math.Clamp(pixels.Y, 0, Math.Max(0, (int)image.Height - 1));
        var right = Math.Clamp(pixels.X + pixels.Width, x + 1, (int)image.Width);
        var bottom = Math.Clamp(pixels.Y + pixels.Height, y + 1, (int)image.Height);
        if (transparent)
        {
            image.Alpha(AlphaOption.Set);
        }

        var fill = transparent ? MagickColors.Transparent : MagickColors.White;
        var drawables = new Drawables().FillColor(fill).StrokeColor(fill);
        if (kind == ImageSelectionKind.Ellipse)
        {
            var originX = (x + right - 1) / 2.0;
            var originY = (y + bottom - 1) / 2.0;
            var radiusX = Math.Max(0.5, (right - x) / 2.0);
            var radiusY = Math.Max(0.5, (bottom - y) / 2.0);
            drawables.Ellipse(originX, originY, radiusX, radiusY, 0, 360);
        }
        else
        {
            drawables.Rectangle(x, y, right - 1, bottom - 1);
        }

        drawables.Draw(image);
    }

    /// <summary>
    /// Keeps pixels inside an inscribed ellipse; clears outside to transparent (local crop coords).
    /// </summary>
    private static void ApplyEllipseAlphaMask(MagickImage cropped)
    {
        cropped.Alpha(AlphaOption.Set);
        var w = cropped.Width;
        var h = cropped.Height;
        using var mask = new MagickImage(MagickColors.Transparent, w, h);
        mask.Alpha(AlphaOption.Set);
        var originX = (w - 1) / 2.0;
        var originY = (h - 1) / 2.0;
        var radiusX = Math.Max(0.5, w / 2.0);
        var radiusY = Math.Max(0.5, h / 2.0);
        new Drawables()
            .FillColor(MagickColors.White)
            .Ellipse(originX, originY, radiusX, radiusY, 0, 360)
            .Draw(mask);
        cropped.Composite(mask, CompositeOperator.DstIn);
    }

    private static void PasteRectCore(
        MagickImage image,
        ImagePixelBuffer source,
        int destinationX,
        int destinationY)
    {
        var settings = new PixelReadSettings((uint)source.Width, (uint)source.Height, StorageType.Char, "BGRA");
        using var overlay = new MagickImage();
        overlay.ReadPixels(source.BgraPixels, settings);
        var x = Math.Clamp(destinationX, 0, Math.Max(0, (int)image.Width - 1));
        var y = Math.Clamp(destinationY, 0, Math.Max(0, (int)image.Height - 1));
        image.Composite(overlay, x, y, CompositeOperator.Over);
    }

    /// <summary>
    /// Tone-curve CLUT: +Shadows lifts darks, −Highlights recovers / darkens brights (and vice versa).
    /// </summary>
    private static void ApplyShadowsHighlights(MagickImage image, double shadows, double highlights)
    {
        using var clut = new MagickImage(MagickColors.Black, 256, 1);
        clut.Depth = 8;
        clut.ColorType = ColorType.TrueColor;
        var pixels = new byte[256 * 4];
        for (var i = 0; i < 256; i++)
        {
            var t = i / 255.0;
            var shadowWeight = (1.0 - t) * (1.0 - t);
            var highlightWeight = t * t;
            var delta = (shadows / 100.0) * 48.0 * shadowWeight
                + (highlights / 100.0) * 48.0 * highlightWeight;
            var outByte = (byte)Math.Clamp((int)Math.Round(i + delta), 0, 255);
            var o = i * 4;
            pixels[o] = outByte;
            pixels[o + 1] = outByte;
            pixels[o + 2] = outByte;
            pixels[o + 3] = 255;
        }

        clut.ReadPixels(pixels, new PixelReadSettings(256, 1, StorageType.Char, "BGRA"));
        image.Clut(clut);
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
