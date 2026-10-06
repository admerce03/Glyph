namespace Glyph.Infrastructure.Session;

public sealed class SessionWindowState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public List<string> Paths { get; set; } = [];

    public int ActiveIndex { get; set; }
}

public sealed class SessionState
{
    /// <summary>Legacy flat list (single-window). Migrated into <see cref="Windows"/> on load/save.</summary>
    public List<string> Paths { get; set; } = [];

    public int ActiveIndex { get; set; }

    /// <summary>One entry per Glyph window; preferred over flat <see cref="Paths"/>.</summary>
    public List<SessionWindowState> Windows { get; set; } = [];

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public interface ISessionStore
{
    Task<SessionState?> TryLoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SessionState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge one window's tab list into the shared session file (multi-window restore).
    /// Empty <paramref name="paths"/> removes that window entry.
    /// </summary>
    Task UpsertWindowAsync(
        string windowId,
        IReadOnlyList<string> paths,
        int activeIndex,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
