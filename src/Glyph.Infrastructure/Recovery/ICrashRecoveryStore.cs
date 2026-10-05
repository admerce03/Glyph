namespace Glyph.Infrastructure.Recovery;

public sealed record CrashRecoveryEntry(
    string DocumentPath,
    string SnapshotPath,
    DateTimeOffset SavedAtUtc,
    long ByteLength);

public interface ICrashRecoveryStore
{
    Task<CrashRecoveryEntry> SaveSnapshotAsync(
        string documentPath,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CrashRecoveryEntry>> ListAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(string snapshotPath, CancellationToken cancellationToken = default);

    Task ClearAllAsync(CancellationToken cancellationToken = default);
}
