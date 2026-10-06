using System.Text.Json;
using Glyph.Core.IO;

namespace Glyph.Infrastructure.RecentFiles;

public sealed class JsonRecentFilesStore : IRecentFilesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly Func<int> _capacityProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<RecentFileEntry>? _cache;

    public JsonRecentFilesStore(string filePath, int capacity = 20)
        : this(filePath, () => capacity)
    {
    }

    public JsonRecentFilesStore(string filePath, Func<int> capacityProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(capacityProvider);

        _filePath = filePath;
        _capacityProvider = capacityProvider;
    }

    public IReadOnlyList<RecentFileEntry> GetRecent()
    {
        _gate.Wait();
        try
        {
            return LoadUnlocked().ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = System.IO.Path.GetFullPath(path);
        var kind = FileFormatDetector.DetectKind(fullPath);
        var displayName = System.IO.Path.GetFileName(fullPath);
        var entry = new RecentFileEntry(fullPath, kind, DateTimeOffset.UtcNow, displayName);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = LoadUnlocked();
            items.RemoveAll(e => string.Equals(e.Path, fullPath, StringComparison.OrdinalIgnoreCase));
            items.Insert(0, entry);
            TrimUnlocked(persistIfTrimmed: false);
            _cache = items;
            await SaveUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cache = [];
            await SaveUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private int ResolveCapacity() => Math.Clamp(_capacityProvider(), 1, 100);

    private List<RecentFileEntry> LoadUnlocked()
    {
        if (_cache is not null)
        {
            TrimUnlocked(persistIfTrimmed: true);
            return _cache;
        }

        if (!File.Exists(_filePath))
        {
            _cache = [];
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            _cache = JsonSerializer.Deserialize<List<RecentFileEntry>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            _cache = [];
        }

        TrimUnlocked(persistIfTrimmed: true);
        return _cache;
    }

    private void TrimUnlocked(bool persistIfTrimmed)
    {
        if (_cache is null)
        {
            return;
        }

        var capacity = ResolveCapacity();
        if (_cache.Count <= capacity)
        {
            return;
        }

        _cache.RemoveRange(capacity, _cache.Count - capacity);
        if (persistIfTrimmed)
        {
            SaveUnlocked();
        }
    }

    private void SaveUnlocked()
    {
        var directory = System.IO.Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_cache ?? [], JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    private async Task SaveUnlockedAsync(CancellationToken cancellationToken)
    {
        var directory = System.IO.Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_cache ?? [], JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
    }
}
