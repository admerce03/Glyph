namespace Glyph.Infrastructure.RecentFiles;

public interface IRecentFilesStore
{
    IReadOnlyList<RecentFileEntry> GetRecent();

    Task AddAsync(string path, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
