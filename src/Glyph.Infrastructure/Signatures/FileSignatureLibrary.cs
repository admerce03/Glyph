using System.Text.Json;
using Glyph.Core.Signatures;

namespace Glyph.Infrastructure.Signatures;

public sealed class FileSignatureLibrary : ISignatureLibrary
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _directory;
    private readonly string _indexPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileSignatureLibrary(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _indexPath = Path.Combine(directory, "index.json");
    }

    public async Task<IReadOnlyList<SignatureEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadIndexAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SignatureEntry> SaveAsync(
        string name,
        Stream pngStream,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(pngStream);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_directory);
            var id = Guid.NewGuid().ToString("N");
            var fileName = id + ".png";
            var path = Path.Combine(_directory, fileName);
            await using (var file = File.Create(path))
            {
                await pngStream.CopyToAsync(file, cancellationToken);
            }

            var entry = new SignatureEntry(
                id,
                name.Trim(),
                fileName,
                DateTimeOffset.UtcNow,
                (description ?? string.Empty).Trim());
            var list = (await ReadIndexAsync(cancellationToken)).ToList();
            list.Add(entry);
            await WriteIndexAsync(list, cancellationToken);
            return entry;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateDescriptionAsync(
        string id,
        string description,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var list = (await ReadIndexAsync(cancellationToken)).ToList();
            var index = list.FindIndex(e => e.Id == id);
            if (index < 0)
            {
                throw new FileNotFoundException($"Signature '{id}' was not found.");
            }

            var existing = list[index];
            list[index] = existing with { Description = (description ?? string.Empty).Trim() };
            await WriteIndexAsync(list, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Stream> OpenImageAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entry = (await ReadIndexAsync(cancellationToken)).FirstOrDefault(e => e.Id == id)
                ?? throw new FileNotFoundException($"Signature '{id}' was not found.");
            var path = Path.Combine(_directory, entry.FileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Signature image missing: {path}");
            }

            // Caller owns the stream.
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var list = (await ReadIndexAsync(cancellationToken)).ToList();
            var entry = list.FirstOrDefault(e => e.Id == id);
            if (entry is null)
            {
                return;
            }

            list.Remove(entry);
            await WriteIndexAsync(list, cancellationToken);
            var path = Path.Combine(_directory, entry.FileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReorderAsync(IReadOnlyList<string> orderedIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await ReadIndexAsync(cancellationToken);
            if (current.Count == 0)
            {
                if (orderedIds.Count != 0)
                {
                    throw new ArgumentException("Cannot reorder an empty signature library.", nameof(orderedIds));
                }

                return;
            }

            if (orderedIds.Count != current.Count || orderedIds.Distinct(StringComparer.Ordinal).Count() != orderedIds.Count)
            {
                throw new ArgumentException(
                    "orderedIds must be a permutation of the current signature ids.",
                    nameof(orderedIds));
            }

            var byId = current.ToDictionary(e => e.Id, StringComparer.Ordinal);
            var reordered = new List<SignatureEntry>(orderedIds.Count);
            foreach (var id in orderedIds)
            {
                if (!byId.TryGetValue(id, out var entry))
                {
                    throw new ArgumentException($"Unknown signature id '{id}'.", nameof(orderedIds));
                }

                reordered.Add(entry);
            }

            await WriteIndexAsync(reordered, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<SignatureEntry>> ReadIndexAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_indexPath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_indexPath);
        var list = await JsonSerializer.DeserializeAsync<List<SignatureEntry>>(stream, JsonOptions, cancellationToken);
        if (list is null)
        {
            return [];
        }

        // Normalize legacy index rows that omit description.
        return list
            .Select(e => e with { Description = e.Description ?? string.Empty })
            .ToList();
    }

    private async Task WriteIndexAsync(IReadOnlyList<SignatureEntry> entries, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);
        await using var stream = File.Create(_indexPath);
        await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, cancellationToken);
    }
}
