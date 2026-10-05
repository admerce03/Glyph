using System.Text.Json;
using Glyph.Core.Documents;

namespace Glyph.Infrastructure.Documents;

public sealed class JsonDocumentViewStateStore : IDocumentViewStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly int _capacity;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, DocumentViewStateEntry>? _cache;

    public JsonDocumentViewStateStore(string filePath, int capacity = 200)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _filePath = filePath;
        _capacity = capacity;
    }

    public async Task<DocumentViewState?> TryLoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var key = Normalize(path);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var map = LoadUnlocked();
            if (!map.TryGetValue(key, out var entry))
            {
                return null;
            }

            return new DocumentViewState
            {
                Zoom = entry.Zoom,
                PageLayout = entry.PageLayout,
                CurrentPageIndex = Math.Max(0, entry.CurrentPageIndex),
                Bookmarks = entry.Bookmarks?
                    .Select(b => new UserBookmark
                    {
                        Id = string.IsNullOrWhiteSpace(b.Id) ? Guid.NewGuid().ToString("N") : b.Id,
                        Title = b.Title ?? string.Empty,
                        PageIndex = Math.Max(0, b.PageIndex),
                    })
                    .ToList() ?? [],
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(string path, DocumentViewState state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(state);
        var key = Normalize(path);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var map = LoadUnlocked();
            map[key] = new DocumentViewStateEntry
            {
                Path = key,
                Zoom = state.Zoom,
                PageLayout = state.PageLayout,
                CurrentPageIndex = Math.Max(0, state.CurrentPageIndex),
                Bookmarks = state.Bookmarks
                    .Select(b => new UserBookmark
                    {
                        Id = b.Id,
                        Title = b.Title,
                        PageIndex = b.PageIndex,
                    })
                    .ToList(),
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };

            if (map.Count > _capacity)
            {
                foreach (var stale in map.Values
                             .OrderBy(e => e.UpdatedAtUtc)
                             .Take(map.Count - _capacity)
                             .Select(e => e.Path)
                             .ToList())
                {
                    map.Remove(stale);
                }
            }

            await SaveUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string Normalize(string path) => System.IO.Path.GetFullPath(path);

    private Dictionary<string, DocumentViewStateEntry> LoadUnlocked()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        if (!File.Exists(_filePath))
        {
            _cache = new Dictionary<string, DocumentViewStateEntry>(StringComparer.OrdinalIgnoreCase);
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<DocumentViewStateEntry>>(json, JsonOptions) ?? [];
            _cache = list
                .Where(e => !string.IsNullOrWhiteSpace(e.Path))
                .GroupBy(e => Normalize(e.Path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.UpdatedAtUtc).First(), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            _cache = new Dictionary<string, DocumentViewStateEntry>(StringComparer.OrdinalIgnoreCase);
        }

        return _cache;
    }

    private async Task SaveUnlockedAsync(CancellationToken cancellationToken)
    {
        var directory = System.IO.Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var list = (_cache ?? []).Values.OrderByDescending(e => e.UpdatedAtUtc).ToList();
        var json = JsonSerializer.Serialize(list, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
    }
}
