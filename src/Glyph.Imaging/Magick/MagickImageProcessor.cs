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

                var blackPoint = Math.Clamp(adjustments.BlackPoint, 0, 100);
                var whitePoint = Math.Clamp(adjustments.WhitePoint, 0, 100);
                var gamma = Math.Clamp(adjustments.Gamma, 0.1, 3.0);
                if (blackPoint > 0.0001 || whitePoint < 99.9999 || Math.Abs(gamma - 1.0) > 0.0001)
                {
                    if (whitePoint <= blackPoint)
                    {
                        whitePoint = Math.Min(100, blackPoint + 1);
                    }

                    magick.Native.Level(new Percentage(blackPoint), new Percentage(whitePoint), gamma);
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

    public Task SetDescriptiveMetadataAsync(
        IImageDocument document,
        ImageDescriptiveMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var iptc = magick.Native.GetIptcProfile() ?? new IptcProfile();
                SetOrClearIptc(iptc, IptcTag.Title, metadata.Title);
                SetOrClearIptc(iptc, IptcTag.Caption, metadata.Description);
                SetOrClearIptc(iptc, IptcTag.CopyrightNotice, metadata.Copyright);

                iptc.RemoveValue(IptcTag.Keyword);
                if (!string.IsNullOrWhiteSpace(metadata.Keywords))
                {
                    foreach (var keyword in metadata.Keywords.Split(
                                 [',', ';'],
                                 StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (keyword.Length > 0)
                        {
                            iptc.SetValue(IptcTag.Keyword, keyword);
                        }
                    }
                }

                magick.Native.SetProfile(iptc);
            },
            cancellationToken);
    }

    public Task AssignColorProfileAsync(
        IImageDocument document,
        ImageColorProfileKind profile,
        CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                magick.Native.SetProfile(ResolveColorProfile(profile));
            },
            cancellationToken);
    }

    public Task ConvertColorProfileAsync(
        IImageDocument document,
        ImageColorProfileKind profile,
        CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = ResolveColorProfile(profile);
                var source = magick.Native.GetColorProfile();
                if (source is null)
                {
                    magick.Native.SetProfile(ColorProfiles.SRGB);
                    source = magick.Native.GetColorProfile() ?? ColorProfiles.SRGB;
                }

                magick.Native.TransformColorSpace(source, destination);
            },
            cancellationToken);
    }

    private static IColorProfile ResolveColorProfile(ImageColorProfileKind profile) =>
        profile switch
        {
            ImageColorProfileKind.AdobeRgb => ColorProfiles.AdobeRGB1998,
            _ => ColorProfiles.SRGB,
        };

    private static void SetOrClearIptc(IIptcProfile iptc, IptcTag tag, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            iptc.RemoveValue(tag);
            return;
        }

        iptc.SetValue(tag, value.Trim());
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
        IReadOnlyList<ImageMarkupPoint>? polygon = null,
        bool inverted = false,
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
                if (inverted)
                {
                    ClearOutsideCore(magick.Native, pixels, transparent, kind, polygon);
                }
                else
                {
                    ClearRegionCore(magick.Native, pixels, transparent, kind, polygon);
                }
            },
            cancellationToken);
    }

    public Task<ImagePixelBuffer> ExtractRectAsync(
        IImageDocument document,
        ImageRect pixels,
        ImageSelectionKind kind = ImageSelectionKind.Rectangle,
        IReadOnlyList<ImageMarkupPoint>? polygon = null,
        bool inverted = false,
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
                return inverted
                    ? ExtractInvertedCore(magick.Native, pixels, kind, polygon)
                    : ExtractRegionCore(magick.Native, pixels, kind, polygon);
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
        IReadOnlyList<ImageMarkupPoint>? polygon = null,
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

                var buffer = ExtractRegionCore(magick.Native, source, kind, polygon);
                ClearRegionCore(magick.Native, source, transparent: true, kind, polygon);
                PasteRectCore(magick.Native, buffer, destinationX, destinationY);
            },
            cancellationToken);
    }

    private static ImagePixelBuffer ExtractRegionCore(
        MagickImage image,
        ImageRect pixels,
        ImageSelectionKind kind,
        IReadOnlyList<ImageMarkupPoint>? polygon)
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
        else if ((kind == ImageSelectionKind.Freeform || kind == ImageSelectionKind.Smart)
            && polygon is { Count: >= 3 })
        {
            ApplyPolygonAlphaMask(clone, pixels, polygon);
        }

        var bgra = clone.ToByteArray(MagickFormat.Bgra);
        return new ImagePixelBuffer(checked((int)clone.Width), checked((int)clone.Height), bgra);
    }

    /// <summary>
    /// Full-image extract with the positive selection punched to transparent.
    /// </summary>
    private static ImagePixelBuffer ExtractInvertedCore(
        MagickImage image,
        ImageRect pixels,
        ImageSelectionKind kind,
        IReadOnlyList<ImageMarkupPoint>? polygon)
    {
        using var clone = (MagickImage)image.Clone();
        clone.Depth = 8;
        clone.ColorType = ColorType.TrueColorAlpha;
        clone.Alpha(AlphaOption.Set);
        PunchSelectionAlpha(clone, pixels, kind, polygon);
        var bgra = clone.ToByteArray(MagickFormat.Bgra);
        return new ImagePixelBuffer(checked((int)clone.Width), checked((int)clone.Height), bgra);
    }

    /// <summary>
    /// Clears everything outside the selection (keeps selected pixels).
    /// </summary>
    private static void ClearOutsideCore(
        MagickImage image,
        ImageRect pixels,
        bool transparent,
        ImageSelectionKind kind,
        IReadOnlyList<ImageMarkupPoint>? polygon)
    {
        var kept = ExtractRegionCore(image, pixels, kind, polygon);
        if (transparent)
        {
            image.Alpha(AlphaOption.Set);
            using var blank = new MagickImage(MagickColors.Transparent, image.Width, image.Height);
            blank.Alpha(AlphaOption.Set);
            image.Composite(blank, CompositeOperator.Copy);
        }
        else
        {
            new Drawables()
                .FillColor(MagickColors.White)
                .StrokeColor(MagickColors.White)
                .Rectangle(0, 0, (int)image.Width - 1, (int)image.Height - 1)
                .Draw(image);
        }

        PasteRectCore(image, kept, pixels.X, pixels.Y);
    }

    private static void ClearRegionCore(
        MagickImage image,
        ImageRect pixels,
        bool transparent,
        ImageSelectionKind kind,
        IReadOnlyList<ImageMarkupPoint>? polygon)
    {
        if (transparent)
        {
            image.Alpha(AlphaOption.Set);
            PunchSelectionAlpha(image, pixels, kind, polygon);
            return;
        }

        var x = Math.Clamp(pixels.X, 0, Math.Max(0, (int)image.Width - 1));
        var y = Math.Clamp(pixels.Y, 0, Math.Max(0, (int)image.Height - 1));
        var right = Math.Clamp(pixels.X + pixels.Width, x + 1, (int)image.Width);
        var bottom = Math.Clamp(pixels.Y + pixels.Height, y + 1, (int)image.Height);
        var drawables = new Drawables().FillColor(MagickColors.White).StrokeColor(MagickColors.White);
        DrawSelectionShape(drawables, x, y, right, bottom, kind, polygon).Draw(image);
    }

    /// <summary>
    /// Punches the selection geometry to transparent via DstOut (drawing Transparent is a no-op).
    /// </summary>
    private static void PunchSelectionAlpha(
        MagickImage image,
        ImageRect pixels,
        ImageSelectionKind kind,
        IReadOnlyList<ImageMarkupPoint>? polygon)
    {
        var x = Math.Clamp(pixels.X, 0, Math.Max(0, (int)image.Width - 1));
        var y = Math.Clamp(pixels.Y, 0, Math.Max(0, (int)image.Height - 1));
        var right = Math.Clamp(pixels.X + pixels.Width, x + 1, (int)image.Width);
        var bottom = Math.Clamp(pixels.Y + pixels.Height, y + 1, (int)image.Height);
        using var mask = new MagickImage(MagickColors.Transparent, image.Width, image.Height);
        mask.Alpha(AlphaOption.Set);
        var drawables = new Drawables().FillColor(MagickColors.White);
        DrawSelectionShape(drawables, x, y, right, bottom, kind, polygon).Draw(mask);
        image.Composite(mask, CompositeOperator.DstOut);
    }

    private static IDrawables<ushort> DrawSelectionShape(
        IDrawables<ushort> drawables,
        int x,
        int y,
        int right,
        int bottom,
        ImageSelectionKind kind,
        IReadOnlyList<ImageMarkupPoint>? polygon)
    {
        if (kind == ImageSelectionKind.Ellipse)
        {
            var originX = (x + right - 1) / 2.0;
            var originY = (y + bottom - 1) / 2.0;
            var radiusX = Math.Max(0.5, (right - x) / 2.0);
            var radiusY = Math.Max(0.5, (bottom - y) / 2.0);
            return drawables.Ellipse(originX, originY, radiusX, radiusY, 0, 360);
        }

        if ((kind == ImageSelectionKind.Freeform || kind == ImageSelectionKind.Smart)
            && polygon is { Count: >= 3 })
        {
            var coords = polygon.Select(p => new PointD(p.X, p.Y)).ToArray();
            return drawables.Polygon(coords);
        }

        return drawables.Rectangle(x, y, right - 1, bottom - 1);
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

    private static void ApplyPolygonAlphaMask(
        MagickImage cropped,
        ImageRect bounds,
        IReadOnlyList<ImageMarkupPoint> polygon)
    {
        cropped.Alpha(AlphaOption.Set);
        using var mask = new MagickImage(MagickColors.Transparent, cropped.Width, cropped.Height);
        mask.Alpha(AlphaOption.Set);
        var local = polygon
            .Select(p => new PointD(p.X - bounds.X, p.Y - bounds.Y))
            .ToArray();
        new Drawables()
            .FillColor(MagickColors.White)
            .Polygon(local)
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

    public Task FlattenMarkupAsync(
        IImageDocument document,
        ImageMarkupLayer layer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layer);
        var magick = RequireMagick(document);
        if (layer.IsEmpty)
        {
            return Task.CompletedTask;
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var stroke in layer.Strokes)
                {
                    if (stroke.Points.Count < 2)
                    {
                        continue;
                    }

                    var color = MagickColor.FromRgba(stroke.R, stroke.G, stroke.B, stroke.A);
                    var coords = stroke.Points
                        .Select(p => new PointD(p.X, p.Y))
                        .ToArray();
                    new Drawables()
                        .StrokeColor(color)
                        .StrokeWidth(stroke.WidthPixels)
                        .StrokeLineCap(LineCap.Round)
                        .StrokeLineJoin(LineJoin.Round)
                        .FillColor(MagickColors.Transparent)
                        .Polyline(coords)
                        .Draw(magick.Native);
                }

                foreach (var shape in layer.Shapes)
                {
                    DrawMarkupShape(magick.Native, shape);
                }
            },
            cancellationToken);
    }

    private static void DrawMarkupShape(MagickImage image, ImageMarkupShape shape)
    {
        var color = MagickColor.FromRgba(shape.R, shape.G, shape.B, shape.A);
        var drawables = new Drawables()
            .StrokeColor(color)
            .StrokeWidth(shape.WidthPixels)
            .StrokeLineCap(LineCap.Round)
            .StrokeLineJoin(LineJoin.Round)
            .FillColor(MagickColors.Transparent);

        var x1 = shape.X1;
        var y1 = shape.Y1;
        var x2 = shape.X2;
        var y2 = shape.Y2;
        switch (shape.Kind)
        {
            case ImageMarkupShapeKind.Rectangle:
                drawables.Rectangle(
                    Math.Min(x1, x2),
                    Math.Min(y1, y2),
                    Math.Max(x1, x2),
                    Math.Max(y1, y2));
                break;
            case ImageMarkupShapeKind.Ellipse:
            {
                var left = Math.Min(x1, x2);
                var top = Math.Min(y1, y2);
                var right = Math.Max(x1, x2);
                var bottom = Math.Max(y1, y2);
                var originX = (left + right) / 2.0;
                var originY = (top + bottom) / 2.0;
                var radiusX = Math.Max(0.5, (right - left) / 2.0);
                var radiusY = Math.Max(0.5, (bottom - top) / 2.0);
                drawables.Ellipse(originX, originY, radiusX, radiusY, 0, 360);
                break;
            }
            case ImageMarkupShapeKind.Line:
                drawables.Line(x1, y1, x2, y2);
                break;
            case ImageMarkupShapeKind.Arrow:
            {
                drawables.Line(x1, y1, x2, y2);
                var angle = Math.Atan2(y2 - y1, x2 - x1);
                var head = Math.Max(8, shape.WidthPixels * 4);
                var a1 = angle + Math.PI - (Math.PI / 6);
                var a2 = angle + Math.PI + (Math.PI / 6);
                drawables
                    .Line(x2, y2, x2 + (head * Math.Cos(a1)), y2 + (head * Math.Sin(a1)))
                    .Line(x2, y2, x2 + (head * Math.Cos(a2)), y2 + (head * Math.Sin(a2)));
                break;
            }
            case ImageMarkupShapeKind.Text:
            {
                var text = string.IsNullOrWhiteSpace(shape.Text) ? "Text" : shape.Text;
                try
                {
                    new Drawables()
                        .FillColor(color)
                        .StrokeColor(MagickColors.Transparent)
                        .Font(MarkupFont.Value)
                        .FontPointSize(shape.FontSizePixels)
                        .Text(x1, y1 + shape.FontSizePixels, text)
                        .Draw(image);
                }
                catch (MagickException)
                {
                    // No usable FreeType font in this environment — keep dimensions via a stub box.
                    new Drawables()
                        .StrokeColor(color)
                        .StrokeWidth(1)
                        .FillColor(MagickColors.Transparent)
                        .Rectangle(x1, y1, x1 + Math.Max(8, text.Length * shape.FontSizePixels * 0.5), y1 + shape.FontSizePixels)
                        .Draw(image);
                }

                return;
            }
            case ImageMarkupShapeKind.Callout:
            {
                var left = Math.Min(x1, x2);
                var top = Math.Min(y1, y2);
                var right = Math.Max(x1, x2);
                var bottom = Math.Max(y1, y2);
                var midX = (left + right) / 2.0;
                var tipY = bottom + Math.Max(12, (bottom - top) * 0.25);
                var tipSpread = Math.Max(8, (right - left) * 0.12);
                var label = string.IsNullOrWhiteSpace(shape.Text) ? "Callout" : shape.Text;
                var callout = new Drawables()
                    .StrokeColor(color)
                    .StrokeWidth(shape.WidthPixels)
                    .FillColor(MagickColors.Transparent)
                    .Rectangle(left, top, right, bottom)
                    .Line(midX - tipSpread, bottom, midX, tipY)
                    .Line(midX + tipSpread, bottom, midX, tipY);
                try
                {
                    callout
                        .FillColor(color)
                        .StrokeColor(MagickColors.Transparent)
                        .Font(MarkupFont.Value)
                        .FontPointSize(shape.FontSizePixels)
                        .Text(left + 4, top + shape.FontSizePixels + 2, label)
                        .Draw(image);
                }
                catch (MagickException)
                {
                    callout.Draw(image);
                }

                return;
            }
        }

        drawables.Draw(image);
    }

    private static readonly Lazy<string> MarkupFont = new(ResolveMarkupFont);

    /// <summary>
    /// Prefer a font that exists on Ubuntu CI runners; fall back to ImageMagick defaults.
    /// </summary>
    private static string ResolveMarkupFont()
    {
        foreach (var candidate in new[] { "DejaVu-Sans", "Liberation-Sans", "Arial", "Helvetica" })
        {
            try
            {
                using var probe = new MagickImage(MagickColors.Transparent, 8, 8);
                new Drawables().Font(candidate).FontPointSize(8).Text(1, 7, "A").Draw(probe);
                return candidate;
            }
            catch (MagickException)
            {
                // try next
            }
        }

        return "sans";
    }

    public Task PasteFileAsync(
        IImageDocument document,
        string sourcePath,
        int destinationX,
        int destinationY,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var overlay = new MagickImage(sourcePath);
                overlay.Alpha(AlphaOption.Set);
                var x = Math.Clamp(destinationX, 0, Math.Max(0, (int)magick.Native.Width - 1));
                var y = Math.Clamp(destinationY, 0, Math.Max(0, (int)magick.Native.Height - 1));
                magick.Native.Composite(overlay, x, y, CompositeOperator.Over);
            },
            cancellationToken);
    }

    public Task RemoveBackgroundAsync(
        IImageDocument document,
        double fuzzPercent = 12,
        CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        var fuzz = Math.Clamp(fuzzPercent, 0, 100);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                RemoveBackgroundCore(magick.Native, fuzz);
            },
            cancellationToken);
    }

    public Task TrimTransparentAsync(IImageDocument document, CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                TrimTransparentCore(magick.Native);
            },
            cancellationToken);
    }

    public Task DeskewAsync(
        IImageDocument document,
        double thresholdPercent = 40,
        bool crop = true,
        CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        var threshold = Math.Clamp(thresholdPercent, 0, 100);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pct = new Percentage(threshold);
                if (crop)
                {
                    magick.Native.DeskewAndCrop(pct);
                }
                else
                {
                    magick.Native.Deskew(pct);
                }
            },
            cancellationToken);
    }

    internal static void RemoveBackgroundCore(MagickImage image, double fuzzPercent)
    {
        image.Alpha(AlphaOption.Set);
        image.ColorFuzz = new Percentage(fuzzPercent);
        var w = checked((int)image.Width);
        var h = checked((int)image.Height);
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var transparent = MagickColors.Transparent;
        // Flood from each corner so connected background regions go transparent.
        image.FloodFill(transparent, 0, 0);
        if (w > 1)
        {
            image.FloodFill(transparent, w - 1, 0);
        }

        if (h > 1)
        {
            image.FloodFill(transparent, 0, h - 1);
        }

        if (w > 1 && h > 1)
        {
            image.FloodFill(transparent, w - 1, h - 1);
        }
    }

    internal static void TrimTransparentCore(MagickImage image)
    {
        image.Alpha(AlphaOption.Set);
        var w = checked((int)image.Width);
        var h = checked((int)image.Height);
        if (w <= 0 || h <= 0)
        {
            return;
        }

        using var clone = image.Clone();
        clone.Depth = 8;
        clone.ColorType = ColorType.TrueColorAlpha;
        var bgra = clone.ToByteArray(MagickFormat.Bgra);
        var minX = w;
        var minY = h;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < h; y++)
        {
            var row = y * w * 4;
            for (var x = 0; x < w; x++)
            {
                var a = bgra[row + x * 4 + 3];
                if (a == 0)
                {
                    continue;
                }

                if (x < minX)
                {
                    minX = x;
                }

                if (y < minY)
                {
                    minY = y;
                }

                if (x > maxX)
                {
                    maxX = x;
                }

                if (y > maxY)
                {
                    maxY = y;
                }
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return;
        }

        if (minX == 0 && minY == 0 && maxX == w - 1 && maxY == h - 1)
        {
            return;
        }

        var cropW = maxX - minX + 1;
        var cropH = maxY - minY + 1;
        image.Crop(new MagickGeometry(minX, minY, (uint)cropW, (uint)cropH));
        image.ResetPage();
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
