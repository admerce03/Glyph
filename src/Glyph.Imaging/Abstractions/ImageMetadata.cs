namespace Glyph.Imaging.Abstractions;

public sealed record ImageMetadata(
    int PixelWidth,
    int PixelHeight,
    long PixelCount,
    double? DensityX,
    double? DensityY,
    int? BitDepth,
    string? ColorSpace,
    string? IccProfileName,
    string FormatName,
    string? Compression,
    long? FileSizeBytes,
    string? CameraMake,
    string? CameraModel,
    string? Lens,
    string? ExposureTime,
    string? FNumber,
    string? Iso,
    string? FocalLength,
    string? DateTaken,
    string? Orientation,
    double? GpsLatitude,
    double? GpsLongitude,
    string? Title,
    string? Description,
    string? Keywords,
    string? Copyright);

public interface IImageMetadataService
{
    Task<ImageMetadata> GetAsync(IImageDocument document, CancellationToken cancellationToken = default);

    Task SetAsync(
        IImageDocument document,
        string? title,
        string? description,
        string? keywords,
        string? copyright,
        CancellationToken cancellationToken = default);

    Task StripGpsAsync(IImageDocument document, CancellationToken cancellationToken = default);

    Task StripAllProfilesAndExifAsync(IImageDocument document, CancellationToken cancellationToken = default);
}

public sealed record ImageColorProfileInfo(bool HasEmbeddedProfile, string? ProfileName, string? ColorSpace);

public interface IImageColorProfileService
{
    Task<ImageColorProfileInfo> GetAsync(IImageDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts the image to sRGB using Magick's colorspace transform when an embedded profile exists.
    /// </summary>
    Task ConvertToSrgbAsync(IImageDocument document, CancellationToken cancellationToken = default);
}

public enum ImageBatchOperationKind
{
    Resize,
    Rotate,
    FlipHorizontal,
    FlipVertical,
    ConvertFormat,
    StripMetadata,
}

public sealed record ImageBatchRequest(
    IReadOnlyList<string> SourcePaths,
    string OutputDirectory,
    ImageBatchOperationKind Operation,
    int? Width = null,
    int? Height = null,
    int? RotateDegrees = null,
    ImageEncodeFormat? OutputFormat = null);

public sealed record ImageBatchProgress(int Completed, int Total, string CurrentPath, string Status);

public sealed record ImageBatchResult(int Succeeded, int Failed, IReadOnlyList<string> OutputPaths, IReadOnlyList<string> Errors);

public interface IImageBatchService
{
    Task<ImageBatchResult> RunAsync(
        ImageBatchRequest request,
        IProgress<ImageBatchProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record ScannerDevice(string Id, string Name, bool IsEmulated);

public sealed record ScanRequest(
    string DeviceId,
    string OutputPath,
    int Dpi = 150,
    bool Grayscale = false);

public interface IScannerService
{
    Task<IReadOnlyList<ScannerDevice>> ListDevicesAsync(CancellationToken cancellationToken = default);

    Task ScanAsync(ScanRequest request, CancellationToken cancellationToken = default);
}
