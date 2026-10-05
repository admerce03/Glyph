using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Storage;

namespace Glyph.App.Scanning;

public sealed record ScannerDeviceInfo(string Id, string Name);

public sealed record ScannerOptions(
    ImageScannerScanSource Source = ImageScannerScanSource.Default,
    ImageScannerColorMode ColorMode = ImageScannerColorMode.Color,
    uint Dpi = 300,
    bool Duplex = false,
    bool AutoCrop = false,
    int? Brightness = null,
    int? Contrast = null,
    uint MaxPages = 1);

/// <summary>
/// Windows.Devices.Scanners wrapper for discovery and scan-to-folder (F42).
/// </summary>
public static class ScannerCaptureHelper
{
    public static async Task<IReadOnlyList<ScannerDeviceInfo>> DiscoverAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var selector = ImageScanner.GetDeviceSelector();
        var devices = await DeviceInformation.FindAllAsync(selector);
        return devices
            .Select(d => new ScannerDeviceInfo(d.Id, string.IsNullOrWhiteSpace(d.Name) ? d.Id : d.Name))
            .ToList();
    }

    public static async Task<IReadOnlyList<StorageFile>> ScanToFolderAsync(
        string deviceId,
        StorageFolder folder,
        ScannerOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var scanner = await ImageScanner.FromIdAsync(deviceId);
        var source = ResolveSource(scanner, options.Source);
        ApplyOptions(scanner, source, options);

        var result = await scanner.ScanFilesToFolderAsync(source, folder);
        return result.ScannedFiles?.ToList() ?? [];
    }

    private static ImageScannerScanSource ResolveSource(ImageScanner scanner, ImageScannerScanSource requested)
    {
        if (requested != ImageScannerScanSource.Default && scanner.IsScanSourceSupported(requested))
        {
            return requested;
        }

        if (scanner.IsScanSourceSupported(ImageScannerScanSource.Flatbed))
        {
            return ImageScannerScanSource.Flatbed;
        }

        if (scanner.IsScanSourceSupported(ImageScannerScanSource.Feeder))
        {
            return ImageScannerScanSource.Feeder;
        }

        if (scanner.IsScanSourceSupported(ImageScannerScanSource.AutoConfigured))
        {
            return ImageScannerScanSource.AutoConfigured;
        }

        return scanner.DefaultScanSource;
    }

    private static void ApplyOptions(ImageScanner scanner, ImageScannerScanSource source, ScannerOptions options)
    {
        IImageScannerSourceConfiguration? config = source switch
        {
            ImageScannerScanSource.Flatbed => scanner.FlatbedConfiguration,
            ImageScannerScanSource.Feeder => scanner.FeederConfiguration,
            ImageScannerScanSource.AutoConfigured => null,
            _ => scanner.IsScanSourceSupported(ImageScannerScanSource.Flatbed)
                ? scanner.FlatbedConfiguration
                : scanner.IsScanSourceSupported(ImageScannerScanSource.Feeder)
                    ? scanner.FeederConfiguration
                    : null,
        };

        if (config is null)
        {
            return;
        }

        try
        {
            config.ColorMode = options.ColorMode;
        }
        catch
        {
            // Device may not support the requested color mode.
        }

        try
        {
            var dpi = Math.Clamp(options.Dpi, 75, 1200);
            config.DesiredResolution = new ImageScannerResolution { DpiX = dpi, DpiY = dpi };
        }
        catch
        {
            // Resolution unsupported.
        }

        try
        {
            config.AutoCroppingMode = options.AutoCrop
                ? ImageScannerAutoCroppingMode.SingleRegion
                : ImageScannerAutoCroppingMode.Disabled;
        }
        catch
        {
            // Auto-crop unsupported.
        }

        if (options.Brightness is int brightness)
        {
            try
            {
                config.Brightness = Math.Clamp(brightness, config.MinBrightness, config.MaxBrightness);
            }
            catch
            {
                // ignore
            }
        }

        if (options.Contrast is int contrast)
        {
            try
            {
                config.Contrast = Math.Clamp(contrast, config.MinContrast, config.MaxContrast);
            }
            catch
            {
                // ignore
            }
        }

        if (source == ImageScannerScanSource.Feeder)
        {
            var feeder = scanner.FeederConfiguration;
            try
            {
                if (feeder.CanScanDuplex)
                {
                    feeder.Duplex = options.Duplex;
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                feeder.MaxNumberOfPages = Math.Max(1, options.MaxPages);
            }
            catch
            {
                // ignore
            }
        }
    }
}
