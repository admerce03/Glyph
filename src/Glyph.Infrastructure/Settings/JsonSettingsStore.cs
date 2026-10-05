using System.Text.Json;

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

        if (settings.VersionSnapshotCapacity < 1)
        {
            settings.VersionSnapshotCapacity = 1;
        }
        else if (settings.VersionSnapshotCapacity > 50)
        {
            settings.VersionSnapshotCapacity = 50;
        }
    }

    private static AppSettings Clone(AppSettings settings) => new()
    {
        Theme = settings.Theme,
        RecentFileCapacity = settings.RecentFileCapacity,
        RestorePreviousSession = settings.RestorePreviousSession,
        AutoSaveToOriginal = settings.AutoSaveToOriginal,
        CrashRecoveryIntervalSeconds = settings.CrashRecoveryIntervalSeconds,
        SidebarWidth = settings.SidebarWidth,
        VersionSnapshotsEnabled = settings.VersionSnapshotsEnabled,
        VersionSnapshotCapacity = settings.VersionSnapshotCapacity,
        OpenFilesInSeparateWindows = settings.OpenFilesInSeparateWindows,
        SidebarVisible = settings.SidebarVisible,
    };
}
