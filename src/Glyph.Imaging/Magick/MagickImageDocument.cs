using Glyph.Imaging.Abstractions;
using ImageMagick;

namespace Glyph.Imaging.Magick;

public sealed class MagickImageDocument : IImageDocument
{
    private MagickImage _image;
    private bool _disposed;

    internal MagickImageDocument(string? path, MagickImage image)
    {
        Path = path;
        _image = image;
    }

    public string? Path { get; set; }

    public int PixelWidth => checked((int)_image.Width);

    public int PixelHeight => checked((int)_image.Height);

    public string FormatName => _image.Format.ToString();

    internal MagickImage Native
    {
        get
        {
            ThrowIfDisposed();
            return _image;
        }
    }

    internal void Replace(MagickImage image)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(image);
        if (!ReferenceEquals(_image, image))
        {
            _image.Dispose();
            _image = image;
        }
    }

    public Task<ImagePixelBuffer> GetPixelsAsync(int? maxEdge = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var clone = _image.Clone();
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
                // Emit 8-bit BGRA32 even when Magick.NET is built as Q16.
                clone.Depth = 8;
                clone.ColorType = ColorType.TrueColorAlpha;
                var pixels = clone.ToByteArray(MagickFormat.Bgra);
                return new ImagePixelBuffer(checked((int)clone.Width), checked((int)clone.Height), pixels);
            },
            cancellationToken);
    }

    public Task<ImageMetadataInfo> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ReadMetadata(_image, Path);
            },
            cancellationToken);
    }

    public IImageEditCheckpoint CaptureCheckpoint()
    {
        ThrowIfDisposed();
        return new MagickImageEditCheckpoint((MagickImage)_image.Clone());
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
            entries);
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

        _image.Dispose();
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
