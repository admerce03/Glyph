namespace Glyph.Infrastructure.Session;

public sealed class SessionState
{
    public List<string> Paths { get; set; } = [];

    public int ActiveIndex { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public interface ISessionStore
{
    Task<SessionState?> TryLoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SessionState state, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
