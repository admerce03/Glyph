using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Magick;

public sealed class MagickImageBatchService : IImageBatchService
{
    private readonly IImageDecoder _decoder;
    private readonly IImageProcessor _processor;
    private readonly IImageEncoder _encoder;
    private readonly IImageMetadataService _metadata;

    public MagickImageBatchService(
        IImageDecoder decoder,
        IImageProcessor processor,
        IImageEncoder encoder,
        IImageMetadataService metadata)
    {
        _decoder = decoder;
        _processor = processor;
        _encoder = encoder;
        _metadata = metadata;
    }

    public async Task<ImageBatchResult> RunAsync(
        ImageBatchRequest request,
        IProgress<ImageBatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SourcePaths.Count == 0)
        {
            return new ImageBatchResult(0, 0, [], []);
        }

        Directory.CreateDirectory(request.OutputDirectory);
        var outputs = new List<string>();
        var errors = new List<string>();
        var total = request.SourcePaths.Count;

        for (var i = 0; i < request.SourcePaths.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = request.SourcePaths[i];
            progress?.Report(new ImageBatchProgress(i, total, source, "Processing"));
            try
            {
                await using var document = await _decoder.OpenAsync(source, cancellationToken);
                switch (request.Operation)
                {
                    case ImageBatchOperationKind.Resize:
                        var width = request.Width ?? document.PixelWidth;
                        var height = request.Height ?? document.PixelHeight;
                        await _processor.ResizeAsync(document, width, height, cancellationToken);
                        break;
                    case ImageBatchOperationKind.Rotate:
                        await _processor.RotateAsync(document, request.RotateDegrees ?? 90, cancellationToken);
                        break;
                    case ImageBatchOperationKind.FlipHorizontal:
                        await _processor.FlipHorizontalAsync(document, cancellationToken);
                        break;
                    case ImageBatchOperationKind.FlipVertical:
                        await _processor.FlipVerticalAsync(document, cancellationToken);
                        break;
                    case ImageBatchOperationKind.StripMetadata:
                        await _metadata.StripAllProfilesAndExifAsync(document, cancellationToken);
                        break;
                    case ImageBatchOperationKind.ConvertFormat:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(request), request.Operation, "Unsupported batch operation.");
                }

                var format = request.OutputFormat
                    ?? InferFormat(source)
                    ?? ImageEncodeFormat.Png;
                var destName = Path.GetFileNameWithoutExtension(source) + ExtensionFor(format);
                var destPath = Path.Combine(request.OutputDirectory, destName);
                await _encoder.SaveAsAsync(document, destPath, format, cancellationToken);
                outputs.Add(destPath);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"{source}: {ex.Message}");
            }

            progress?.Report(new ImageBatchProgress(i + 1, total, source, errors.Count > 0 && errors[^1].StartsWith(source, StringComparison.Ordinal) ? "Failed" : "Done"));
        }

        return new ImageBatchResult(outputs.Count, errors.Count, outputs, errors);
    }

    private static ImageEncodeFormat? InferFormat(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => ImageEncodeFormat.Png,
            ".jpg" or ".jpeg" => ImageEncodeFormat.Jpeg,
            ".webp" => ImageEncodeFormat.Webp,
            ".bmp" => ImageEncodeFormat.Bmp,
            ".tif" or ".tiff" => ImageEncodeFormat.Tiff,
            ".gif" => ImageEncodeFormat.Gif,
            _ => null,
        };

    private static string ExtensionFor(ImageEncodeFormat format) => format switch
    {
        ImageEncodeFormat.Jpeg => ".jpg",
        ImageEncodeFormat.Webp => ".webp",
        ImageEncodeFormat.Bmp => ".bmp",
        ImageEncodeFormat.Tiff => ".tiff",
        ImageEncodeFormat.Gif => ".gif",
        _ => ".png",
    };
}

/// <summary>
/// Emulated scanner for headless/CI environments. Produces a solid PNG "scan".
/// Windows WIA/TWAIN hardware path is deferred until a Windows interactive harness is available.
/// </summary>
public sealed class EmulatedScannerService : IScannerService
{
    public const string EmulatedDeviceId = "emulated-flatbed";

    public Task<IReadOnlyList<ScannerDevice>> ListDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ScannerDevice> devices =
        [
            new ScannerDevice(EmulatedDeviceId, "Glyph Emulated Flatbed", IsEmulated: true),
        ];
        return Task.FromResult(devices);
    }

    public Task ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.DeviceId, EmulatedDeviceId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unknown scanner device.", nameof(request));
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dir = Path.GetDirectoryName(request.OutputPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Approximate letter page at requested DPI.
                var width = Math.Max(32, (int)(8.5 * request.Dpi));
                var height = Math.Max(32, (int)(11.0 * request.Dpi));
                using var image = new ImageMagick.MagickImage(
                    request.Grayscale ? ImageMagick.MagickColors.White : ImageMagick.MagickColors.GhostWhite,
                    (uint)width,
                    (uint)height);
                if (request.Grayscale)
                {
                    image.ColorType = ImageMagick.ColorType.Grayscale;
                }

                image.Density = new ImageMagick.Density(request.Dpi, request.Dpi);
                image.Format = ImageMagick.MagickFormat.Png;
                image.Write(request.OutputPath);
            },
            cancellationToken);
    }
}
