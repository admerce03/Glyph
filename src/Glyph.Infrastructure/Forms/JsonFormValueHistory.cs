using System.Text.Json;

namespace Glyph.Infrastructure.Forms;

public sealed class JsonFormValueHistory : IFormValueHistory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;
    private readonly int _perFieldCapacity;
    private readonly int _globalCapacity;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private FormValueHistoryState? _cache;

    public JsonFormValueHistory(
        string filePath,
        int perFieldCapacity = 12,
        int globalCapacity = 40)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(perFieldCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(globalCapacity, 1);

        _filePath = filePath;
        _perFieldCapacity = perFieldCapacity;
        _globalCapacity = globalCapacity;
    }

    public IReadOnlyList<string> GetSuggestions(string? fieldName, int limit = 8)
    {
        if (limit < 1)
        {
            return [];
        }

        _gate.Wait();
        try
        {
            var state = LoadUnlocked();
            var results = new List<string>();
            var key = NormalizeFieldName(fieldName);
            if (key is not null && state.ByField.TryGetValue(key, out var fieldValues))
            {
                foreach (var value in fieldValues)
                {
                    AddUnique(results, value, limit);
                }
            }

            foreach (var value in state.Global)
            {
                AddUnique(results, value, limit);
            }

            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RememberAsync(string? fieldName, string value, CancellationToken cancellationToken = default)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > 500)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = LoadUnlocked();
            var key = NormalizeFieldName(fieldName);
            if (key is not null)
            {
                if (!state.ByField.TryGetValue(key, out var list))
                {
                    list = [];
                    state.ByField[key] = list;
                }

                PrependUnique(list, trimmed, _perFieldCapacity);
            }

            PrependUnique(state.Global, trimmed, _globalCapacity);
            _cache = state;
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
            _cache = new FormValueHistoryState();
            await SaveUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string? NormalizeFieldName(string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return null;
        }

        return fieldName.Trim().ToLowerInvariant();
    }

    private static void PrependUnique(List<string> list, string value, int capacity)
    {
        list.RemoveAll(v => string.Equals(v, value, StringComparison.Ordinal));
        list.Insert(0, value);
        if (list.Count > capacity)
        {
            list.RemoveRange(capacity, list.Count - capacity);
        }
    }

    private static void AddUnique(List<string> results, string value, int limit)
    {
        if (results.Count >= limit)
        {
            return;
        }

        if (results.Exists(v => string.Equals(v, value, StringComparison.Ordinal)))
        {
            return;
        }

        results.Add(value);
    }

    private FormValueHistoryState LoadUnlocked()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        if (!File.Exists(_filePath))
        {
            _cache = new FormValueHistoryState();
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            _cache = JsonSerializer.Deserialize<FormValueHistoryState>(json, JsonOptions)
                ?? new FormValueHistoryState();
            _cache.ByField ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _cache.Global ??= [];
        }
        catch (JsonException)
        {
            _cache = new FormValueHistoryState();
        }

        return _cache;
    }

    private async Task SaveUnlockedAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_cache ?? new FormValueHistoryState(), JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
    }

    private sealed class FormValueHistoryState
    {
        public Dictionary<string, List<string>> ByField { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<string> Global { get; set; } = [];
    }
}
