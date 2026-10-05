namespace Glyph.Core.Signatures;

public interface ISignatureLibrary
{
    Task<IReadOnlyList<SignatureEntry>> ListAsync(CancellationToken cancellationToken = default);

    Task<SignatureEntry> SaveAsync(
        string name,
        Stream pngStream,
        string? description = null,
        CancellationToken cancellationToken = default);

    Task UpdateDescriptionAsync(
        string id,
        string description,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenImageAsync(string id, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rewrite library order to match <paramref name="orderedIds"/> (must be a permutation of current ids).
    /// </summary>
    Task ReorderAsync(IReadOnlyList<string> orderedIds, CancellationToken cancellationToken = default);
}
