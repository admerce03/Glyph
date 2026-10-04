namespace Glyph.Pdf.Text;

/// <summary>
/// Ensures only the latest Find query is active. Starting a new search cancels
/// any in-flight search so results never go stale.
/// </summary>
public sealed class PdfSearchCoordinator : IDisposable
{
    private readonly IPdfTextSearchService _searchService;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private int _generation;
    private bool _disposed;

    public PdfSearchCoordinator(IPdfTextSearchService searchService)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
    }

    public int Generation => Volatile.Read(ref _generation);

    public async Task<PdfSearchResult> SearchAsync(
        string path,
        string query,
        PdfSearchOptions? options = null,
        CancellationToken externalToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        CancellationTokenSource linked;
        int generation;
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            linked = _cts;
            generation = Interlocked.Increment(ref _generation);
        }

        try
        {
            var result = await _searchService
                .SearchAsync(path, query, options, linked.Token)
                .ConfigureAwait(false);

            if (generation != Volatile.Read(ref _generation))
            {
                return PdfSearchResult.Cancelled();
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return PdfSearchResult.Cancelled();
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            Interlocked.Increment(ref _generation);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
