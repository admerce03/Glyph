using System.Text.Json;

namespace Glyph.Infrastructure.Recovery;

public sealed class FileCrashRecoveryStore : ICrashRecoveryStore
{
    private readonly string _root;
    private readonly string _indexPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public FileCrashRecoveryStore(string recoveryDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryDirectory);
        _root = recoveryDirectory;
        _indexPath = Path.Combine(_root, "index.json");
    }

    public async Task<CrashRecoveryEntry> SaveSnapshotAsync(
        string documentPath,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentNullException.ThrowIfNull(content);
        Directory.CreateDirectory(_root);

        var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(documentPath);
        if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
        {
            fileName += ".bin";
        }

        var snapshotPath = Path.Combine(_root, fileName);
        await using (var output = File.Create(snapshotPath))
        {
            await content.CopyToAsync(output, cancellationToken);
        }

        var entry = new CrashRecoveryEntry(
            documentPath,
            snapshotPath,
            DateTimeOffset.UtcNow,
            new FileInfo(snapshotPath).Length);

        var list = (await ListAsync(cancellationToken)).ToList();
        list.RemoveAll(e => string.Equals(e.DocumentPath, documentPath, StringComparison.OrdinalIgnoreCase));
        list.Add(entry);
        await WriteIndexAsync(list, cancellationToken);
        return entry;
    }

    public async Task<IReadOnlyList<CrashRecoveryEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_indexPath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_indexPath);
        var list = await JsonSerializer.DeserializeAsync<List<CrashRecoveryEntry>>(stream, JsonOptions, cancellationToken)
            ?? [];
        return list.Where(e => File.Exists(e.SnapshotPath)).OrderByDescending(e => e.SavedAtUtc).ToList();
    }

    public async Task DeleteAsync(string snapshotPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotPath);
        if (File.Exists(snapshotPath))
        {
            File.Delete(snapshotPath);
        }

        var list = (await ListAsync(cancellationToken))
            .Where(e => !string.Equals(e.SnapshotPath, snapshotPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        await WriteIndexAsync(list, cancellationToken);
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await ListAsync(cancellationToken);
        foreach (var entry in list)
        {
            if (File.Exists(entry.SnapshotPath))
            {
                File.Delete(entry.SnapshotPath);
            }
        }

        await WriteIndexAsync([], cancellationToken);
    }

    private async Task WriteIndexAsync(IReadOnlyList<CrashRecoveryEntry> entries, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);
        await using var stream = File.Create(_indexPath);
        await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, cancellationToken);
    }
}
