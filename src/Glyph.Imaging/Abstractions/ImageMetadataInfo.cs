namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Snapshot of image properties and common EXIF/GPS fields.
/// </summary>
public sealed record ImageMetadataInfo(
    int PixelWidth,
    int PixelHeight,
    long? FileSizeBytes,
    string FormatName,
    double? DpiX,
    double? DpiY,
    int? BitDepth,
    string? ColorSpace,
    bool HasIccProfile,
    string? Compression,
    string? Make,
    string? Model,
    string? LensModel,
    string? ExposureTime,
    string? Aperture,
    string? Iso,
    string? FocalLength,
    string? CaptureDate,
    string? Orientation,
    double? GpsLatitude,
    double? GpsLongitude,
    IReadOnlyList<ImageMetadataEntry> Entries);

public sealed record ImageMetadataEntry(string Group, string Name, string Value);
