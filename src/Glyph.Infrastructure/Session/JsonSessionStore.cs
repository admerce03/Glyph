using System.Text.Json;

namespace Glyph.Infrastructure.Session;

public sealed class JsonSessionStore : ISessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonSessionStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public async Task<SessionState?> TryLoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadNormalizedUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(SessionState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            NormalizeInPlace(state, requireExistingFiles: false);
            if (state.Windows.Count == 0 && state.Paths.Count == 0)
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }

                return;
            }

            await WriteUnlockedAsync(state, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpsertWindowAsync(
        string windowId,
        IReadOnlyList<string> paths,
        int activeIndex,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(windowId);
        ArgumentNullException.ThrowIfNull(paths);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await ReadNormalizedUnlockedAsync(cancellationToken).ConfigureAwait(false)
                ?? new SessionState();

            NormalizeInPlace(state, requireExistingFiles: false);

            var cleaned = paths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            state.Windows.RemoveAll(w =>
                string.Equals(w.Id, windowId, StringComparison.OrdinalIgnoreCase));

            if (cleaned.Count > 0)
            {
                state.Windows.Add(new SessionWindowState
                {
                    Id = windowId,
                    Paths = cleaned,
                    ActiveIndex = Math.Clamp(activeIndex, 0, cleaned.Count - 1),
                });
            }

            SyncLegacyFlatList(state);
            if (state.Windows.Count == 0)
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }

                return;
            }

            await WriteUnlockedAsync(state, cancellationToken).ConfigureAwait(false);
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
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SessionState?> ReadNormalizedUnlockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
        var state = JsonSerializer.Deserialize<SessionState>(json, JsonOptions);
        if (state is null)
        {
            return null;
        }

        NormalizeInPlace(state, requireExistingFiles: true);
        if (state.Windows.Count == 0)
        {
            return null;
        }

        return state;
    }

    private async Task WriteUnlockedAsync(SessionState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        state.UpdatedAtUtc = DateTimeOffset.UtcNow;
        SyncLegacyFlatList(state);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Prefer <see cref="SessionState.Windows"/>; migrate legacy flat Paths into a single window.
    /// </summary>
    public static void NormalizeInPlace(SessionState state, bool requireExistingFiles)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Windows ??= [];
        state.Paths ??= [];

        if (state.Windows.Count == 0 && state.Paths.Count > 0)
        {
            state.Windows.Add(new SessionWindowState
            {
                Id = "legacy",
                Paths = [.. state.Paths],
                ActiveIndex = state.ActiveIndex,
            });
        }

        foreach (var window in state.Windows)
        {
            window.Id = string.IsNullOrWhiteSpace(window.Id)
                ? Guid.NewGuid().ToString("N")
                : window.Id;
            window.Paths = (window.Paths ?? [])
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(p => !requireExistingFiles || File.Exists(p))
                .ToList();
            window.ActiveIndex = window.Paths.Count == 0
                ? 0
                : Math.Clamp(window.ActiveIndex, 0, window.Paths.Count - 1);
        }

        state.Windows.RemoveAll(w => w.Paths.Count == 0);
        SyncLegacyFlatList(state);
    }

    private static void SyncLegacyFlatList(SessionState state)
    {
        if (state.Windows.Count == 0)
        {
            state.Paths = [];
            state.ActiveIndex = 0;
            return;
        }

        // Flat list remains a de-duplicated union for older readers / status text.
        state.Paths = state.Windows
            .SelectMany(w => w.Paths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var primary = state.Windows[0];
        if (primary.Paths.Count == 0)
        {
            state.ActiveIndex = 0;
            return;
        }

        var activePath = primary.Paths[Math.Clamp(primary.ActiveIndex, 0, primary.Paths.Count - 1)];
        var idx = state.Paths.FindIndex(p =>
            string.Equals(p, activePath, StringComparison.OrdinalIgnoreCase));
        state.ActiveIndex = idx >= 0 ? idx : 0;
    }
}
