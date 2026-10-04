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

    /// <summary>
    /// Appends (or inserts) every page from each source document into <paramref name="document"/>
    /// in order, starting at <paramref name="insertIndex"/> (PageCount = append).
    /// </summary>
    Task MergeDocumentsAsync(
        IPdfDocument document,
        IReadOnlyList<IPdfDocument> sources,
        int insertIndex,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Splits <paramref name="document"/> into contiguous page ranges. Each index in
    /// <paramref name="splitBeforeIndexes"/> starts a new document (0 is implied).
    /// </summary>
    Task<IReadOnlyList<IPdfDocument>> SplitDocumentAsync(
        IPdfDocument document,
        IReadOnlyList<int> splitBeforeIndexes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a non-destructive CropBox on each selected page by insetting
    /// <paramref name="margins"/> from the page MediaBox (falling back to the current CropBox).
    /// Underlying page content is preserved.
    /// </summary>
    Task CropPagesAsync(
        IPdfDocument document,
        IReadOnlyList<int> pageIndexes,
        PdfCropMargins margins,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets an absolute CropBox on each selected page (PDF user space points).
    /// </summary>
    Task SetCropBoxAsync(
        IPdfDocument document,
        IReadOnlyList<int> pageIndexes,
        PdfCropBox cropBox,
        CancellationToken cancellationToken = default);

    Task SaveAsync(IPdfDocument document, string path, CancellationToken cancellationToken = default);

    Task<byte[]> SaveToBytesAsync(IPdfDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the in-memory page tree with a prior PDF snapshot (used for undo/redo).
    /// </summary>
    Task RestoreAsync(IPdfDocument document, byte[] pdfBytes, CancellationToken cancellationToken = default);
}
