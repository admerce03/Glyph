using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Graphics.Printing;
using Windows.Storage;

namespace Glyph.App.Scanning;

public sealed record ScannerDeviceInfo(string Id, string Name);

public sealed record ScannerOptions(
    ImageScannerScanSource Source = ImageScannerScanSource.Default,
    ImageScannerColorMode ColorMode = ImageScannerColorMode.Color,
    uint Dpi = 300,
    bool Duplex = false,
    bool AutoCrop = false,
    /// <summary>When true with AutoCrop, prefer MultipleRegion (separate photos on flatbed).</summary>
    bool MultiPhoto = false,
    int? Brightness = null,
    int? Contrast = null,
    uint MaxPages = 1,
    PrintMediaSize PageSize = PrintMediaSize.Default,
    bool AutoDetectPageSize = false);

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

    /// <summary>Common paper sizes in inches (width × height, portrait).</summary>
    public static bool TryGetPaperSizeInches(PrintMediaSize size, out double widthInches, out double heightInches)
    {
        switch (size)
        {
            case PrintMediaSize.NorthAmericaLetter:
                widthInches = 8.5;
                heightInches = 11;
                return true;
            case PrintMediaSize.NorthAmericaLegal:
                widthInches = 8.5;
                heightInches = 14;
                return true;
            case PrintMediaSize.NorthAmericaTabloid:
                widthInches = 11;
                heightInches = 17;
                return true;
            case PrintMediaSize.NorthAmericaStatement:
                widthInches = 5.5;
                heightInches = 8.5;
                return true;
            case PrintMediaSize.IsoA3:
                widthInches = 11.69;
                heightInches = 16.54;
                return true;
            case PrintMediaSize.IsoA4:
                widthInches = 8.27;
                heightInches = 11.69;
                return true;
            case PrintMediaSize.IsoA5:
                widthInches = 5.83;
                heightInches = 8.27;
                return true;
            default:
                widthInches = 0;
                heightInches = 0;
                return false;
        }
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
            var mode = options.AutoCrop
                ? (options.MultiPhoto
                    ? ImageScannerAutoCroppingMode.MultipleRegion
                    : ImageScannerAutoCroppingMode.SingleRegion)
                : ImageScannerAutoCroppingMode.Disabled;
            if (mode != ImageScannerAutoCroppingMode.Disabled
                && !config.IsAutoCroppingModeSupported(mode)
                && mode == ImageScannerAutoCroppingMode.MultipleRegion
                && config.IsAutoCroppingModeSupported(ImageScannerAutoCroppingMode.SingleRegion))
            {
                mode = ImageScannerAutoCroppingMode.SingleRegion;
            }

            config.AutoCroppingMode = mode;
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

        ApplyPaperSize(scanner, source, config, options);

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

    private static void ApplyPaperSize(
        ImageScanner scanner,
        ImageScannerScanSource source,
        IImageScannerSourceConfiguration config,
        ScannerOptions options)
    {
        if (source == ImageScannerScanSource.Feeder)
        {
            var feeder = scanner.FeederConfiguration;
            try
            {
                if (options.AutoDetectPageSize && feeder.CanAutoDetectPageSize)
                {
                    feeder.AutoDetectPageSize = true;
                    return;
                }

                if (options.AutoDetectPageSize == false && feeder.CanAutoDetectPageSize)
                {
                    feeder.AutoDetectPageSize = false;
                }
            }
            catch
            {
                // ignore
            }

            if (options.PageSize == PrintMediaSize.Default)
            {
                return;
            }

            try
            {
                if (feeder.IsPageSizeSupported(options.PageSize, PrintOrientation.Default)
                    || feeder.IsPageSizeSupported(options.PageSize, PrintOrientation.Portrait))
                {
                    feeder.PageSize = options.PageSize;
                    try
                    {
                        feeder.PageOrientation = PrintOrientation.Portrait;
                    }
                    catch
                    {
                        // Orientation may be fixed.
                    }

                    return;
                }
            }
            catch
            {
                // PageSize unsupported — fall through to region.
            }
        }

        // Flatbed (and feeder fallback): set SelectedScanRegion from known paper inches.
        // Region is ignored when AutoCroppingMode is not Disabled.
        if (options.AutoCrop
            || options.PageSize == PrintMediaSize.Default
            || !TryGetPaperSizeInches(options.PageSize, out var width, out var height))
        {
            return;
        }

        try
        {
            var max = config.MaxScanArea;
            var w = Math.Min(width, max.Width);
            var h = Math.Min(height, max.Height);
            if (w > 0 && h > 0)
            {
                config.SelectedScanRegion = new Rect(0, 0, w, h);
            }
        }
        catch
        {
            // Region unsupported.
        }
    }
}
