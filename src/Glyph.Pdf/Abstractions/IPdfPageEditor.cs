namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Page-tree editing operations. Concrete engines implement this behind the same document model
/// used for viewing so DnD/reorder workflows stay first-class.
/// </summary>
public interface IPdfPageEditor
{
    Task RotatePagesAsync(IPdfDocument document, IReadOnlyList<int> pageIndexes, int deltaDegrees, CancellationToken cancellationToken = default);

    Task DeletePagesAsync(IPdfDocument document, IReadOnlyList<int> pageIndexes, CancellationToken cancellationToken = default);

    Task ReorderPagesAsync(IPdfDocument document, IReadOnlyList<int> newOrder, CancellationToken cancellationToken = default);

    Task<IPdfDocument> ExtractPagesAsync(IPdfDocument document, IReadOnlyList<int> pageIndexes, CancellationToken cancellationToken = default);
}
