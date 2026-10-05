using Glyph.Imaging.Abstractions;
using ImageMagick;

namespace Glyph.Imaging.Magick;

public sealed class MagickImageDocument : IImageDocument
{
    private readonly List<MagickImage> _frames;
    private int _frameIndex;
    private readonly int _animationIterations;
    private bool _disposed;

    internal MagickImageDocument(string? path, MagickImage image)
        : this(path, [image])
    {
    }

    internal MagickImageDocument(string? path, IReadOnlyList<MagickImage> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0)
        {
            throw new ArgumentException("At least one frame is required.", nameof(frames));
        }

        Path = path;
        _frames = [.. frames];
        _frameIndex = 0;
        _animationIterations = frames.Count > 1
            ? checked((int)frames[0].AnimationIterations)
            : 1;
        ColorManagedDisplay = true;
        SoftProofProfile = null;
        DisplayRenderingIntent = ImageRenderingIntent.Perceptual;
    }

    public string? Path { get; set; }

    public int PixelWidth => checked((int)Current.Width);

    public int PixelHeight => checked((int)Current.Height);

    public string FormatName => Current.Format.ToString();

    public int FrameCount => _frames.Count;

    public int CurrentFrameIndex => _frameIndex;

    public int AnimationIterations => _animationIterations;

    public bool ColorManagedDisplay { get; set; }

    public ImageColorProfileKind? SoftProofProfile { get; set; }

    public ImageRenderingIntent DisplayRenderingIntent { get; set; }

    internal MagickImage Native
    {
        get
        {
            ThrowIfDisposed();
            return Current;
        }
    }

    private MagickImage Current
    {
        get
        {
            ThrowIfDisposed();
            return _frames[_frameIndex];
        }
    }

    internal void Replace(MagickImage image)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(image);
        var existing = _frames[_frameIndex];
        if (!ReferenceEquals(existing, image))
        {
            existing.Dispose();
            _frames[_frameIndex] = image;
        }
    }

    public int GetFrameDelayMilliseconds(int frameIndex)
    {
        ThrowIfDisposed();
        EnsureFrameIndex(frameIndex);
        var frame = _frames[frameIndex];
        var ticksPerSecond = frame.AnimationTicksPerSecond <= 0 ? 100 : (int)frame.AnimationTicksPerSecond;
        var delayTicks = frame.AnimationDelay;
        // GIF delay 0 is treated as ~10cs (100ms) by most browsers.
        if (delayTicks == 0)
        {
            return 100;
        }

        return Math.Max(1, (int)Math.Round(delayTicks * 1000.0 / ticksPerSecond));
    }

    public Task SetCurrentFrameAsync(int frameIndex, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureFrameIndex(frameIndex);
        cancellationToken.ThrowIfCancellationRequested();
        _frameIndex = frameIndex;
        return Task.CompletedTask;
    }

    public Task<ImagePixelBuffer> ExtractFrameAsync(int frameIndex, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureFrameIndex(frameIndex);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Extract returns stored pixels (no display color management).
                return ToBgraBuffer(
                    _frames[frameIndex],
                    maxEdge: null,
                    colorManagedDisplay: false,
                    softProof: null,
                    renderingIntent: ImageRenderingIntent.Perceptual);
            },
            cancellationToken);
    }

    public Task<ImagePixelBuffer> GetPixelsAsync(int? maxEdge = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var frame = Current;
        var managed = ColorManagedDisplay;
        var softProof = SoftProofProfile;
        var intent = DisplayRenderingIntent;
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ToBgraBuffer(frame, maxEdge, managed, softProof, intent);
            },
            cancellationToken);
    }

    public Task<ImageMetadataInfo> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var frame = Current;
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var meta = ReadMetadata(frame, Path);
                if (_frames.Count > 1)
                {
                    var entries = meta.Entries.ToList();
                    entries.Insert(0, new("Animation", "Frames", _frames.Count.ToString()));
                    entries.Insert(1, new("Animation", "Current frame", (_frameIndex + 1).ToString()));
                    entries.Insert(
                        2,
                        new(
                            "Animation",
                            "Loop",
                            _animationIterations == 0 ? "Infinite" : _animationIterations.ToString()));
                    return meta with { Entries = entries };
                }

                return meta;
            },
            cancellationToken);
    }

    public IImageEditCheckpoint CaptureCheckpoint()
    {
        ThrowIfDisposed();
        return new MagickImageEditCheckpoint((MagickImage)Current.Clone());
    }

    public void RestoreCheckpoint(IImageEditCheckpoint checkpoint)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (checkpoint is not MagickImageEditCheckpoint magickCheckpoint)
        {
            throw new ArgumentException("Checkpoint was not created by this document type.", nameof(checkpoint));
        }

        Replace(magickCheckpoint.TakeOwnership());
        checkpoint.Dispose();
    }

    private static ImagePixelBuffer ToBgraBuffer(
        MagickImage source,
        int? maxEdge,
        bool colorManagedDisplay,
        ImageColorProfileKind? softProof,
        ImageRenderingIntent renderingIntent)
    {
        using var clone = (MagickImage)source.Clone();
        if (maxEdge is int edge && edge > 0)
        {
            var longest = Math.Max(clone.Width, clone.Height);
            if (longest > (uint)edge)
            {
                clone.Resize(new MagickGeometry((uint)edge)
                {
                    Greater = true,
                    IgnoreAspectRatio = false,
                });
            }
        }

        clone.AutoOrient();
        if (colorManagedDisplay)
        {
            ApplyDisplayColorManagement(clone, softProof, renderingIntent);
        }

        // Emit 8-bit BGRA32 even when Magick.NET is built as Q16.
        clone.Depth = 8;
        clone.ColorType = ColorType.TrueColorAlpha;
        var pixels = clone.ToByteArray(MagickFormat.Bgra);
        return new ImagePixelBuffer(checked((int)clone.Width), checked((int)clone.Height), pixels);
    }

    private static void ApplyDisplayColorManagement(
        MagickImage image,
        ImageColorProfileKind? softProof,
        ImageRenderingIntent renderingIntent)
    {
        var source = image.GetColorProfile();
        if (source is null && softProof is null)
        {
            return;
        }

        image.RenderingIntent = renderingIntent switch
        {
            ImageRenderingIntent.Relative => RenderingIntent.Relative,
            ImageRenderingIntent.Saturation => RenderingIntent.Saturation,
            ImageRenderingIntent.Absolute => RenderingIntent.Absolute,
            _ => RenderingIntent.Perceptual,
        };

        var srgb = ColorProfiles.SRGB;
        if (softProof is ImageColorProfileKind proofKind)
        {
            var proof = proofKind == ImageColorProfileKind.AdobeRgb
                ? ColorProfiles.AdobeRGB1998
                : ColorProfiles.SRGB;
            if (source is not null)
            {
                image.TransformColorSpace(source, proof);
            }
            else
            {
                image.SetProfile(proof);
            }

            // Soft-proof simulation: proof space → sRGB for the display buffer.
            if (!ReferenceEquals(proof, srgb))
            {
                image.TransformColorSpace(proof, srgb);
            }

            return;
        }

        if (source is not null)
        {
            image.TransformColorSpace(source, srgb);
        }
    }

    private void EnsureFrameIndex(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _frames.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameIndex),
                frameIndex,
                $"Frame index must be between 0 and {_frames.Count - 1}.");
        }
    }

    internal static ImageMetadataInfo ReadMetadata(MagickImage image, string? path)
    {
        long? fileSize = null;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            fileSize = new FileInfo(path).Length;
        }

        double? dpiX = null;
        double? dpiY = null;
        if (image.Density.X > 0 || image.Density.Y > 0)
        {
            var density = image.Density;
            if (density.Units == DensityUnit.PixelsPerCentimeter)
            {
                dpiX = density.X * 2.54;
                dpiY = density.Y * 2.54;
            }
            else
            {
                dpiX = density.X;
                dpiY = density.Y;
            }
        }

        var entries = new List<ImageMetadataEntry>
        {
            new("File", "Format", image.Format.ToString()),
            new("File", "Dimensions", $"{image.Width}×{image.Height}"),
            new("File", "Color space", image.ColorSpace.ToString()),
            new("File", "Bit depth", image.Depth.ToString()),
            new("File", "Compression", image.Compression.ToString()),
        };

        if (fileSize is long bytes)
        {
            entries.Add(new("File", "File size", FormatBytes(bytes)));
        }

        if (dpiX is not null || dpiY is not null)
        {
            entries.Add(new("File", "DPI", $"{dpiX:0.##} × {dpiY:0.##}"));
        }

        var hasIcc = image.GetColorProfile() is not null;
        entries.Add(new("File", "ICC profile", hasIcc ? "Present" : "None"));

        string? make = null;
        string? model = null;
        string? lens = null;
        string? exposure = null;
        string? aperture = null;
        string? iso = null;
        string? focal = null;
        string? captureDate = null;
        string? orientation = null;
        double? gpsLat = null;
        double? gpsLon = null;

        var exif = image.GetExifProfile();
        if (exif is not null)
        {
            make = ReadString(exif, ExifTag.Make);
            model = ReadString(exif, ExifTag.Model);
            lens = ReadString(exif, ExifTag.LensModel);
            exposure = FormatRational(exif.GetValue(ExifTag.ExposureTime)?.Value);
            aperture = FormatRational(exif.GetValue(ExifTag.FNumber)?.Value);
            iso = ReadIso(exif);
            focal = FormatRational(exif.GetValue(ExifTag.FocalLength)?.Value);
            captureDate = ReadString(exif, ExifTag.DateTimeOriginal)
                ?? ReadString(exif, ExifTag.DateTime);
            orientation = exif.GetValue(ExifTag.Orientation)?.Value.ToString();

            AddIfPresent(entries, "EXIF", "Make", make);
            AddIfPresent(entries, "EXIF", "Model", model);
            AddIfPresent(entries, "EXIF", "Lens", lens);
            AddIfPresent(entries, "EXIF", "Exposure", exposure);
            AddIfPresent(entries, "EXIF", "Aperture", aperture is null ? null : "f/" + aperture);
            AddIfPresent(entries, "EXIF", "ISO", iso);
            AddIfPresent(entries, "EXIF", "Focal length", focal is null ? null : focal + " mm");
            AddIfPresent(entries, "EXIF", "Captured", captureDate);
            AddIfPresent(entries, "EXIF", "Orientation", orientation);

            gpsLat = ReadGpsCoordinate(
                exif.GetValue(ExifTag.GPSLatitude)?.Value,
                ReadString(exif, ExifTag.GPSLatitudeRef));
            gpsLon = ReadGpsCoordinate(
                exif.GetValue(ExifTag.GPSLongitude)?.Value,
                ReadString(exif, ExifTag.GPSLongitudeRef));

            if (gpsLat is not null && gpsLon is not null)
            {
                entries.Add(new("GPS", "Latitude", gpsLat.Value.ToString("0.######")));
                entries.Add(new("GPS", "Longitude", gpsLon.Value.ToString("0.######")));
            }
        }

        string? title = null;
        string? description = null;
        string? keywords = null;
        string? copyright = null;
        int? rating = null;

        var iptc = image.GetIptcProfile();
        var hasIptc = iptc is not null;
        if (iptc is not null)
        {
            title = ReadIptc(iptc, IptcTag.Title) ?? ReadIptc(iptc, IptcTag.Headline);
            description = ReadIptc(iptc, IptcTag.Caption);
            copyright = ReadIptc(iptc, IptcTag.CopyrightNotice);
            var keywordValues = iptc.GetAllValues(IptcTag.Keyword)?
                .Select(v => v.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();
            if (keywordValues is { Count: > 0 })
            {
                keywords = string.Join(", ", keywordValues);
            }

            AddIfPresent(entries, "IPTC", "Title", title);
            AddIfPresent(entries, "IPTC", "Description", description);
            AddIfPresent(entries, "IPTC", "Keywords", keywords);
            AddIfPresent(entries, "IPTC", "Copyright", copyright);
            AddIfPresent(entries, "IPTC", "Byline", ReadIptc(iptc, IptcTag.Byline));
        }

        var xmp = image.GetXmpProfile();
        var hasXmp = xmp is not null;
        if (xmp is not null)
        {
            entries.Add(new("XMP", "Profile", "Present"));
            try
            {
                var doc = xmp.ToXDocument();
                if (doc is not null)
                {
                    title ??= FirstXmpText(doc, "title");
                    description ??= FirstXmpText(doc, "description");
                    copyright ??= FirstXmpText(doc, "rights");
                    keywords ??= FirstXmpBagBag(doc, "subject");
                    rating ??= FirstXmpInt(doc, "Rating");
                    AddIfPresent(entries, "XMP", "Title", FirstXmpText(doc, "title"));
                    AddIfPresent(entries, "XMP", "Description", FirstXmpText(doc, "description"));
                    AddIfPresent(entries, "XMP", "Keywords", FirstXmpBagBag(doc, "subject"));
                    AddIfPresent(entries, "XMP", "Copyright", FirstXmpText(doc, "rights"));
                    if (rating is int r)
                    {
                        entries.Add(new("XMP", "Rating", r.ToString()));
                    }
                }
            }
            catch
            {
                // XMP parse is best-effort; presence flag remains.
            }
        }

        // EXIF descriptive tags as last-resort fill for title/description/copyright.
        if (exif is not null)
        {
            title ??= ReadString(exif, ExifTag.ImageDescription);
            copyright ??= ReadString(exif, ExifTag.Copyright);
            AddIfPresent(entries, "EXIF", "Description", ReadString(exif, ExifTag.ImageDescription));
            AddIfPresent(entries, "EXIF", "Copyright", ReadString(exif, ExifTag.Copyright));
        }

        return new ImageMetadataInfo(
            checked((int)image.Width),
            checked((int)image.Height),
            fileSize,
            image.Format.ToString(),
            dpiX,
            dpiY,
            checked((int)image.Depth),
            image.ColorSpace.ToString(),
            hasIcc,
            image.Compression.ToString(),
            make,
            model,
            lens,
            exposure,
            aperture is null ? null : "f/" + aperture,
            iso,
            focal is null ? null : focal + " mm",
            captureDate,
            orientation,
            gpsLat,
            gpsLon,
            title,
            description,
            keywords,
            copyright,
            rating,
            hasIptc,
            hasXmp,
            entries);
    }

    private static string? ReadIptc(IIptcProfile iptc, IptcTag tag)
        => Truncate(iptc.GetValue(tag)?.Value);

    private static string? FirstXmpText(System.Xml.Linq.XDocument doc, string localName)
    {
        var el = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
        if (el is null)
        {
            return null;
        }

        var li = el.Descendants().FirstOrDefault(e => e.Name.LocalName == "li");
        return Truncate((li ?? el).Value);
    }

    private static string? FirstXmpBagBag(System.Xml.Linq.XDocument doc, string localName)
    {
        var el = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
        if (el is null)
        {
            return null;
        }

        var items = el.Descendants()
            .Where(e => e.Name.LocalName == "li")
            .Select(e => e.Value.Trim())
            .Where(v => v.Length > 0)
            .ToList();
        if (items.Count == 0)
        {
            return Truncate(el.Value);
        }

        return Truncate(string.Join(", ", items));
    }

    private static int? FirstXmpInt(System.Xml.Linq.XDocument doc, string localName)
    {
        var el = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
        if (el is null)
        {
            return null;
        }

        return int.TryParse(el.Value.Trim(), out var value) ? value : null;
    }

    private static void AddIfPresent(List<ImageMetadataEntry> entries, string group, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entries.Add(new(group, name, value));
        }
    }

    private static string? ReadString(IExifProfile exif, ExifTag<string> tag)
        => Truncate(exif.GetValue(tag)?.Value);

    private static string? ReadIso(IExifProfile exif)
    {
        var ratings = exif.GetValue(ExifTag.ISOSpeedRatings)?.Value;
        if (ratings is { Length: > 0 })
        {
            return ratings[0].ToString();
        }

        var speed = exif.GetValue(ExifTag.ISOSpeed)?.Value;
        return speed?.ToString();
    }

    private static string? FormatRational(Rational? value)
    {
        if (value is null)
        {
            return null;
        }

        var number = value.Value.ToDouble();
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            return null;
        }

        if (number >= 1)
        {
            return number.ToString("0.##");
        }

        if (number <= 0)
        {
            return number.ToString("0.####");
        }

        return "1/" + Math.Round(1.0 / number).ToString("0");
    }

    private static double? ReadGpsCoordinate(Rational[]? parts, string? hemi)
    {
        if (parts is null || parts.Length < 3)
        {
            return null;
        }

        var degrees = parts[0].ToDouble();
        var minutes = parts[1].ToDouble();
        var seconds = parts[2].ToDouble();
        var decimalDegrees = degrees + (minutes / 60.0) + (seconds / 3600.0);
        if (string.Equals(hemi, "S", StringComparison.OrdinalIgnoreCase)
            || string.Equals(hemi, "W", StringComparison.OrdinalIgnoreCase))
        {
            decimalDegrees = -decimalDegrees;
        }

        return decimalDegrees;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes + " B";
        }

        if (bytes < 1024 * 1024)
        {
            return (bytes / 1024.0).ToString("0.#") + " KB";
        }

        return (bytes / (1024.0 * 1024.0)).ToString("0.##") + " MB";
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim('\0', ' ', '\t', '\r', '\n');
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var frame in _frames)
        {
            frame.Dispose();
        }

        _frames.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
