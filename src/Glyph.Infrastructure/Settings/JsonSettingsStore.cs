using System.Text.Json;
using Glyph.Core.Documents;

namespace Glyph.Infrastructure.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AppSettings _current = new();

    public JsonSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public AppSettings Current => _current;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                _current = new AppSettings();
                return Clone(_current);
            }

            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            _current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            Normalize(_current);
            return Clone(_current);
        }
        catch (JsonException)
        {
            _current = new AppSettings();
            return Clone(_current);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Normalize(settings);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _current = Clone(settings);
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_current, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void Normalize(AppSettings settings)
    {
        if (settings.RecentFileCapacity < 1)
        {
            settings.RecentFileCapacity = 1;
        }
        else if (settings.RecentFileCapacity > 100)
        {
            settings.RecentFileCapacity = 100;
        }

        if (settings.CrashRecoveryIntervalSeconds < 0)
        {
            settings.CrashRecoveryIntervalSeconds = 0;
        }
        else if (settings.CrashRecoveryIntervalSeconds > 3600)
        {
            settings.CrashRecoveryIntervalSeconds = 3600;
        }

        if (double.IsNaN(settings.SidebarWidth) || settings.SidebarWidth < 140)
        {
            settings.SidebarWidth = 140;
        }
        else if (settings.SidebarWidth > 480)
        {
            settings.SidebarWidth = 480;
        }

        if (double.IsNaN(settings.ThumbnailWidth) || settings.ThumbnailWidth < 72)
        {
            settings.ThumbnailWidth = 72;
        }
        else if (settings.ThumbnailWidth > 180)
        {
            settings.ThumbnailWidth = 180;
        }

        if (settings.VersionSnapshotCapacity < 1)
        {
            settings.VersionSnapshotCapacity = 1;
        }
        else if (settings.VersionSnapshotCapacity > 50)
        {
            settings.VersionSnapshotCapacity = 50;
        }

        if (double.IsNaN(settings.DefaultStrokeWidthPoints) || settings.DefaultStrokeWidthPoints < 0.5)
        {
            settings.DefaultStrokeWidthPoints = 0.5;
        }
        else if (settings.DefaultStrokeWidthPoints > 12)
        {
            settings.DefaultStrokeWidthPoints = 12;
        }

        settings.DefaultHighlightColor = string.IsNullOrWhiteSpace(settings.DefaultHighlightColor)
            ? "Yellow"
            : settings.DefaultHighlightColor.Trim();
        settings.DefaultStrokeColor = string.IsNullOrWhiteSpace(settings.DefaultStrokeColor)
            ? "Red"
            : settings.DefaultStrokeColor.Trim();
        settings.DefaultStickyNoteColor = string.IsNullOrWhiteSpace(settings.DefaultStickyNoteColor)
            ? "Yellow"
            : settings.DefaultStickyNoteColor.Trim();

        if (double.IsNaN(settings.DefaultZoom) || settings.DefaultZoom < 0.1)
        {
            settings.DefaultZoom = 0.1;
        }
        else if (settings.DefaultZoom > 8)
        {
            settings.DefaultZoom = 8;
        }

        settings.DefaultPageLayout = NormalizePageLayoutName(settings.DefaultPageLayout);

        settings.Zoom100Meaning = string.Equals(settings.Zoom100Meaning, "Print", StringComparison.OrdinalIgnoreCase)
            ? "Print"
            : "Pixels";

        settings.DefaultInterpolation = settings.DefaultInterpolation?.Trim() switch
        {
            "NearestNeighbor" or "Nearest-neighbor" or "Nearest" => "NearestNeighbor",
            "Bilinear" => "Bilinear",
            "Bicubic" => "Bicubic",
            _ => "Auto",
        };

        // OCR is always on-device; keep the flag true so prefs stay honest.
        settings.LocalOnlyOcr = true;
        settings.OcrLanguageTag = (settings.OcrLanguageTag ?? string.Empty).Trim();
        settings.AnnotationAuthor = (settings.AnnotationAuthor ?? string.Empty).Trim();

        settings.ToolbarHiddenCommands ??= [];
        var known = new HashSet<string>(ToolbarCommands.Catalog.Select(c => c.Id), StringComparer.OrdinalIgnoreCase);
        settings.ToolbarHiddenCommands = settings.ToolbarHiddenCommands
            .Where(id => !string.IsNullOrWhiteSpace(id) && known.Contains(id.Trim()))
            .Select(id => id.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        settings.ShortcutOverrides = ShortcutCustomizationPolicy.NormalizeOverrides(settings.ShortcutOverrides);
    }

    private static string NormalizePageLayoutName(string? name) => name?.Trim() switch
    {
        "Single" or "SinglePage" => "Single",
        "TwoPage" or "Two-up" or "Two page" => "TwoPage",
        "TwoPageWithCover" or "Two-up cover" or "Two page with cover" => "TwoPageWithCover",
        _ => "Continuous",
    };

    private static AppSettings Clone(AppSettings settings) => new()
    {
        Theme = settings.Theme,
        RecentFileCapacity = settings.RecentFileCapacity,
        RestorePreviousSession = settings.RestorePreviousSession,
        AutoSaveToOriginal = settings.AutoSaveToOriginal,
        CrashRecoveryIntervalSeconds = settings.CrashRecoveryIntervalSeconds,
        SidebarWidth = settings.SidebarWidth,
        ThumbnailWidth = settings.ThumbnailWidth,
        VersionSnapshotsEnabled = settings.VersionSnapshotsEnabled,
        VersionSnapshotCapacity = settings.VersionSnapshotCapacity,
        OpenFilesInSeparateWindows = settings.OpenFilesInSeparateWindows,
        AnnotationAuthor = settings.AnnotationAuthor ?? string.Empty,
        CompactToolbar = settings.CompactToolbar,
        ToolbarHiddenCommands = settings.ToolbarHiddenCommands?.ToList() ?? [],
        ShortcutOverrides = settings.ShortcutOverrides is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(settings.ShortcutOverrides, StringComparer.OrdinalIgnoreCase),
        DefaultHighlightColor = settings.DefaultHighlightColor ?? "Yellow",
        DefaultStrokeColor = settings.DefaultStrokeColor ?? "Red",
        DefaultStickyNoteColor = settings.DefaultStickyNoteColor ?? "Yellow",
        DefaultStrokeWidthPoints = settings.DefaultStrokeWidthPoints,
        AnimationAutoplay = settings.AnimationAutoplay,
        StripMetadataByDefault = settings.StripMetadataByDefault,
        DefaultPageLayout = settings.DefaultPageLayout ?? "Continuous",
        DefaultZoom = settings.DefaultZoom,
        Zoom100Meaning = settings.Zoom100Meaning ?? "Pixels",
        DefaultInterpolation = settings.DefaultInterpolation ?? "Auto",
        ColorManagedDisplayDefault = settings.ColorManagedDisplayDefault,
        LocalOnlyOcr = true,
        OcrLanguageTag = settings.OcrLanguageTag ?? string.Empty,
        SidebarVisible = settings.SidebarVisible,
    };
}
