using Glyph.Core.Documents;

namespace Glyph.Infrastructure.Documents;

/// <summary>
/// Persists per-document page/zoom/layout so reopen restores viewing position.
/// </summary>
public interface IDocumentViewStateStore
{
    Task<DocumentViewState?> TryLoadAsync(string path, CancellationToken cancellationToken = default);

    Task SaveAsync(string path, DocumentViewState state, CancellationToken cancellationToken = default);
}
