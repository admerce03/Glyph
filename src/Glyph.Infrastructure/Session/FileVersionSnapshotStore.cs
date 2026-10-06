using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Glyph.Infrastructure.Session;

public sealed class VersionSnapshotEntry
{
    public string Id { get; set; } = string.Empty;

    public string OriginalPath { get; set; } = string.Empty;

    public string SnapshotPath { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public long ByteLength { get; set; }

    public DateTimeOffset SavedAtUtc { get; set; }
}

public interface IVersionSnapshotStore
{
    Task<VersionSnapshotEntry?> CaptureAsync(string originalPath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VersionSnapshotEntry>> ListAsync(string originalPath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string snapshotId, string originalPath, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(string originalPath, CancellationToken cancellationToken = default);
}

/// <summary>Local dated copies of saved documents (F51), capped per original path.</summary>
public sealed class FileVersionSnapshotStore : IVersionSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _root;
    private readonly Func<int> _capacityProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileVersionSnapshotStore(string rootDirectory, int capacity = 5)
        : this(rootDirectory, () => capacity)
    {
    }

    public FileVersionSnapshotStore(string rootDirectory, Func<int> capacityProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(capacityProvider);
        _root = rootDirectory;
        _capacityProvider = capacityProvider;
    }

    private int ResolveCapacity() => Math.Clamp(_capacityProvider(), 1, 50);

    public async Task<VersionSnapshotEntry?> CaptureAsync(string originalPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);
        if (!File.Exists(originalPath))
        {
            return null;
        }

        var id = Guid.NewGuid().ToString("N")[..12];
        var folder = Path.Combine(_root, HashPath(originalPath));
        Directory.CreateDirectory(folder);
        var ext = Path.GetExtension(originalPath);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = ".bin";
        }

        var snapshotPath = Path.Combine(folder, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{id}{ext}");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            File.Copy(originalPath, snapshotPath, overwrite: false);
            var info = new FileInfo(snapshotPath);
            var entry = new VersionSnapshotEntry
            {
                Id = id,
                OriginalPath = Path.GetFullPath(originalPath),
                SnapshotPath = snapshotPath,
                DisplayName = Path.GetFileName(originalPath),
                ByteLength = info.Length,
                SavedAtUtc = DateTimeOffset.UtcNow,
            };
            var metaPath = Path.Combine(folder, id + ".json");
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await File.WriteAllTextAsync(metaPath, json, cancellationToken).ConfigureAwait(false);
            await TrimOverflowAsync(folder, cancellationToken).ConfigureAwait(false);
            return entry;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<VersionSnapshotEntry>> ListAsync(
        string originalPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);
        var folder = Path.Combine(_root, HashPath(originalPath));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadFolderAsync(folder, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string snapshotId, string originalPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);
        var folder = Path.Combine(_root, HashPath(originalPath));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var metaPath = Path.Combine(folder, snapshotId + ".json");
            if (File.Exists(metaPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(metaPath, cancellationToken).ConfigureAwait(false);
                    var entry = JsonSerializer.Deserialize<VersionSnapshotEntry>(json, JsonOptions);
                    if (entry is not null && File.Exists(entry.SnapshotPath))
                    {
                        File.Delete(entry.SnapshotPath);
                    }
                }
                catch (JsonException)
                {
                }

                File.Delete(metaPath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAllAsync(string originalPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);
        var folder = Path.Combine(_root, HashPath(originalPath));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task TrimOverflowAsync(string folder, CancellationToken cancellationToken)
    {
        var entries = await ReadFolderAsync(folder, cancellationToken).ConfigureAwait(false);
        foreach (var old in entries.Skip(ResolveCapacity()))
        {
            try
            {
                if (File.Exists(old.SnapshotPath))
                {
                    File.Delete(old.SnapshotPath);
                }

                var metaPath = Path.Combine(folder, old.Id + ".json");
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private static async Task<IReadOnlyList<VersionSnapshotEntry>> ReadFolderAsync(
        string folder,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var results = new List<VersionSnapshotEntry>();
        foreach (var metaPath in Directory.EnumerateFiles(folder, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var json = await File.ReadAllTextAsync(metaPath, cancellationToken).ConfigureAwait(false);
                var entry = JsonSerializer.Deserialize<VersionSnapshotEntry>(json, JsonOptions);
                if (entry is null || string.IsNullOrWhiteSpace(entry.SnapshotPath) || !File.Exists(entry.SnapshotPath))
                {
                    continue;
                }

                if (entry.ByteLength <= 0)
                {
                    entry.ByteLength = new FileInfo(entry.SnapshotPath).Length;
                }

                results.Add(entry);
            }
            catch (JsonException)
            {
            }
        }

        return results.OrderByDescending(e => e.SavedAtUtc).ToList();
    }

    private static string HashPath(string path)
    {
        var full = Path.GetFullPath(path);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(full.ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
