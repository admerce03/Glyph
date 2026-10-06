namespace Glyph.Infrastructure.Session;

public sealed class SessionWindowState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public List<string> Paths { get; set; } = [];

    public int ActiveIndex { get; set; }

    /// <summary>Screen X of the window (DIP/pixels as reported by AppWindow). 0 with Width=0 means unset.</summary>
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool IsMaximized { get; set; }
}

public sealed class SessionWindowBounds
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool IsMaximized { get; init; }
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
    /// Merge one window's tab list (and optional bounds) into the shared session file.
    /// Empty <paramref name="paths"/> removes that window entry.
    /// </summary>
    Task UpsertWindowAsync(
        string windowId,
        IReadOnlyList<string> paths,
        int activeIndex,
        SessionWindowBounds? bounds = null,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
