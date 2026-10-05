using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Glyph.Infrastructure.Session;

public sealed class RecoveryEntry
{
    public string Id { get; set; } = string.Empty;

    public string OriginalPath { get; set; } = string.Empty;

    public string RecoveryPath { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset SavedAtUtc { get; set; }
}

public interface ICrashRecoveryStore
{
    Task SaveSnapshotAsync(string originalPath, Stream content, string? preferredExtension = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecoveryEntry>> ListAsync(CancellationToken cancellationToken = default);

    Task DiscardAsync(string originalPath, CancellationToken cancellationToken = default);

    Task DiscardAllAsync(CancellationToken cancellationToken = default);
}

public sealed class FileCrashRecoveryStore : ICrashRecoveryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _root;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileCrashRecoveryStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _root = rootDirectory;
    }

    public async Task SaveSnapshotAsync(
        string originalPath,
        Stream content,
        string? preferredExtension = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);
        ArgumentNullException.ThrowIfNull(content);

        var id = HashPath(originalPath);
        var folder = Path.Combine(_root, id);
        Directory.CreateDirectory(folder);

        var ext = preferredExtension
            ?? Path.GetExtension(originalPath);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = ".bin";
        }

        var recoveryPath = Path.Combine(folder, "snapshot" + ext);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (var output = File.Create(recoveryPath))
            {
                content.Seek(0, SeekOrigin.Begin);
                await content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            var entry = new RecoveryEntry
            {
                Id = id,
                OriginalPath = Path.GetFullPath(originalPath),
                RecoveryPath = recoveryPath,
                DisplayName = Path.GetFileName(originalPath),
                SavedAtUtc = DateTimeOffset.UtcNow,
            };
            var metaPath = Path.Combine(folder, "meta.json");
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await File.WriteAllTextAsync(metaPath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<RecoveryEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_root))
            {
                return [];
            }

            var results = new List<RecoveryEntry>();
            foreach (var folder in Directory.EnumerateDirectories(_root))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var metaPath = Path.Combine(folder, "meta.json");
                if (!File.Exists(metaPath))
                {
                    continue;
                }

                try
                {
                    var json = await File.ReadAllTextAsync(metaPath, cancellationToken).ConfigureAwait(false);
                    var entry = JsonSerializer.Deserialize<RecoveryEntry>(json, JsonOptions);
                    if (entry is null || string.IsNullOrWhiteSpace(entry.RecoveryPath) || !File.Exists(entry.RecoveryPath))
                    {
                        continue;
                    }

                    results.Add(entry);
                }
                catch (JsonException)
                {
                    // skip corrupt meta
                }
            }

            return results.OrderByDescending(e => e.SavedAtUtc).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DiscardAsync(string originalPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);
        var id = HashPath(originalPath);
        var folder = Path.Combine(_root, id);
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

    public async Task DiscardAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string HashPath(string path)
    {
        var full = Path.GetFullPath(path);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(full.ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
