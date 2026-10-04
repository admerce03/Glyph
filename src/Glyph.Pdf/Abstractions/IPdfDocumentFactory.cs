namespace Glyph.Pdf.Abstractions;

public interface IPdfDocumentFactory
{
    Task<IPdfDocument> OpenAsync(string path, string? password = null, CancellationToken cancellationToken = default);
}
