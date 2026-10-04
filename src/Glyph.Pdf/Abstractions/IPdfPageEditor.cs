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

    /// <summary>
    /// Inserts a blank page at <paramref name="insertIndex"/> (0 = before first page, PageCount = append).
    /// </summary>
    Task InsertBlankPageAsync(
        IPdfDocument document,
        int insertIndex,
        double widthPoints = 612,
        double heightPoints = 792,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Duplicates the given pages, inserting each copy immediately after its source.
    /// </summary>
    Task DuplicatePagesAsync(IPdfDocument document, IReadOnlyList<int> pageIndexes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts pages from <paramref name="source"/> into <paramref name="document"/> at <paramref name="insertIndex"/>.
    /// </summary>
    Task InsertPagesAsync(
        IPdfDocument document,
        IPdfDocument source,
        IReadOnlyList<int> sourcePageIndexes,
        int insertIndex,
        CancellationToken cancellationToken = default);

    Task SaveAsync(IPdfDocument document, string path, CancellationToken cancellationToken = default);
}
