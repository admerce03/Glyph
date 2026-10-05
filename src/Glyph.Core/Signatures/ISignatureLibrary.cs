namespace Glyph.Core.Signatures;

public interface ISignatureLibrary
{
    Task<IReadOnlyList<SignatureEntry>> ListAsync(CancellationToken cancellationToken = default);

    Task<SignatureEntry> SaveAsync(
        string name,
        Stream pngStream,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenImageAsync(string id, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
