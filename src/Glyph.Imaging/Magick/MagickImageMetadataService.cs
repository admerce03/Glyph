using System.Text;
using Glyph.Imaging.Abstractions;
using ImageMagick;

namespace Glyph.Imaging.Magick;

public sealed class MagickImageMetadataService : IImageMetadataService
{
    public Task<ImageMetadata> GetAsync(IImageDocument document, CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var image = magick.Native;
                long? fileSize = null;
                if (!string.IsNullOrWhiteSpace(magick.Path) && File.Exists(magick.Path))
                {
                    fileSize = new FileInfo(magick.Path).Length;
                }

                var profile = image.GetColorProfile();
                var exif = image.GetExifProfile();
                TryReadGps(exif, out var lat, out var lon);

                return new ImageMetadata(
                    PixelWidth: magick.PixelWidth,
                    PixelHeight: magick.PixelHeight,
                    PixelCount: (long)magick.PixelWidth * magick.PixelHeight,
                    DensityX: image.Density.X,
                    DensityY: image.Density.Y,
                    BitDepth: (int)image.Depth,
                    ColorSpace: image.ColorSpace.ToString(),
                    IccProfileName: profile?.Description ?? profile?.ColorSpace.ToString(),
                    FormatName: magick.FormatName,
                    Compression: image.Compression.ToString(),
                    FileSizeBytes: fileSize,
                    CameraMake: Read(exif, ExifTag.Make),
                    CameraModel: Read(exif, ExifTag.Model),
                    Lens: Read(exif, ExifTag.LensModel) ?? Read(exif, ExifTag.LensMake),
                    ExposureTime: Read(exif, ExifTag.ExposureTime),
                    FNumber: Read(exif, ExifTag.FNumber),
                    Iso: ReadIso(exif),
                    FocalLength: Read(exif, ExifTag.FocalLength),
                    DateTaken: Read(exif, ExifTag.DateTimeOriginal) ?? Read(exif, ExifTag.DateTime),
                    Orientation: image.Orientation.ToString(),
                    GpsLatitude: lat,
                    GpsLongitude: lon,
                    Title: Read(exif, ExifTag.ImageDescription) ?? image.Comment,
                    Description: image.Comment,
                    Keywords: ReadXp(exif, ExifTag.XPKeywords) ?? ReadUserComment(exif),
                    Copyright: Read(exif, ExifTag.Copyright));
            },
            cancellationToken);
    }

    public Task SetAsync(
        IImageDocument document,
        string? title,
        string? description,
        string? keywords,
        string? copyright,
        CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var image = magick.Native;
                if (description is not null || title is not null)
                {
                    image.Comment = description ?? title;
                }

                var profile = image.GetExifProfile() ?? new ExifProfile();
                if (title is not null)
                {
                    profile.SetValue(ExifTag.ImageDescription, title);
                }

                if (copyright is not null)
                {
                    profile.SetValue(ExifTag.Copyright, copyright);
                }

                if (keywords is not null)
                {
                    profile.SetValue(ExifTag.XPKeywords, Encoding.Unicode.GetBytes(keywords + "\0"));
                }

                image.SetProfile(profile);
            },
            cancellationToken);
    }

    public Task StripGpsAsync(IImageDocument document, CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var profile = magick.Native.GetExifProfile();
                if (profile is null)
                {
                    return;
                }

                profile.RemoveValue(ExifTag.GPSLatitude);
                profile.RemoveValue(ExifTag.GPSLatitudeRef);
                profile.RemoveValue(ExifTag.GPSLongitude);
                profile.RemoveValue(ExifTag.GPSLongitudeRef);
                profile.RemoveValue(ExifTag.GPSAltitude);
                profile.RemoveValue(ExifTag.GPSAltitudeRef);
                magick.Native.SetProfile(profile);
            },
            cancellationToken);
    }

    public Task StripAllProfilesAndExifAsync(IImageDocument document, CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                magick.Native.RemoveProfile("exif");
                magick.Native.RemoveProfile("icc");
                magick.Native.RemoveProfile("iptc");
                magick.Native.RemoveProfile("xmp");
                magick.Native.Comment = null;
            },
            cancellationToken);
    }

    private static string? Read<T>(IExifProfile? profile, ExifTag<T> tag)
        where T : notnull
    {
        var entry = profile?.GetValue(tag);
        return entry is null ? null : Convert.ToString(entry.Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? ReadIso(IExifProfile? profile)
    {
        var value = profile?.GetValue(ExifTag.ISOSpeedRatings)?.Value;
        return value is null ? null : string.Join(',', value);
    }

    private static string? ReadXp(IExifProfile? profile, ExifTag<byte[]> tag)
    {
        var bytes = profile?.GetValue(tag)?.Value;
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    }

    private static string? ReadUserComment(IExifProfile? profile)
    {
        var bytes = profile?.GetValue(ExifTag.UserComment)?.Value;
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        // EXIF UserComment often starts with an 8-byte charset prefix.
        if (bytes.Length > 8)
        {
            return Encoding.UTF8.GetString(bytes, 8, bytes.Length - 8).TrimEnd('\0');
        }

        return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
    }

    private static void TryReadGps(IExifProfile? profile, out double? latitude, out double? longitude)
    {
        latitude = null;
        longitude = null;
        if (profile is null)
        {
            return;
        }

        var lat = profile.GetValue(ExifTag.GPSLatitude)?.Value;
        var lon = profile.GetValue(ExifTag.GPSLongitude)?.Value;
        if (lat is null || lon is null)
        {
            return;
        }

        latitude = ToDegrees(lat);
        longitude = ToDegrees(lon);
        var latRef = profile.GetValue(ExifTag.GPSLatitudeRef)?.Value;
        var lonRef = profile.GetValue(ExifTag.GPSLongitudeRef)?.Value;
        if (string.Equals(latRef, "S", StringComparison.OrdinalIgnoreCase) && latitude is not null)
        {
            latitude = -latitude;
        }

        if (string.Equals(lonRef, "W", StringComparison.OrdinalIgnoreCase) && longitude is not null)
        {
            longitude = -longitude;
        }
    }

    private static double? ToDegrees(Rational[] values)
    {
        if (values.Length < 3)
        {
            return null;
        }

        return values[0].ToDouble() + (values[1].ToDouble() / 60.0) + (values[2].ToDouble() / 3600.0);
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

public sealed class MagickImageColorProfileService : IImageColorProfileService
{
    public Task<ImageColorProfileInfo> GetAsync(IImageDocument document, CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var profile = magick.Native.GetColorProfile();
                return new ImageColorProfileInfo(
                    profile is not null,
                    profile?.Description ?? profile?.ColorSpace.ToString(),
                    magick.Native.ColorSpace.ToString());
            },
            cancellationToken);
    }

    public Task ConvertToSrgbAsync(IImageDocument document, CancellationToken cancellationToken = default)
    {
        var magick = RequireMagick(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var image = magick.Native;
                image.TransformColorSpace(ColorProfiles.SRGB);
                image.ColorSpace = ColorSpace.sRGB;
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
