using Glyph.Core.Documents;
using Glyph.Pdf.Abstractions;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumPageEditor : IPdfPageEditor
{
    public Task RotatePagesAsync(
        IPdfDocument document,
        IReadOnlyList<int> pageIndexes,
        int deltaDegrees,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ValidateIndexes(pdfium, pageIndexes);
        if (deltaDegrees % 90 != 0)
        {
            throw new ArgumentException("Rotation delta must be a multiple of 90 degrees.", nameof(deltaDegrees));
        }

        var quarterTurns = ((deltaDegrees / 90) % 4 + 4) % 4;
        if (quarterTurns == 0 || pageIndexes.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    foreach (var index in pageIndexes.Distinct().OrderBy(i => i))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var page = fpdfview.FPDF_LoadPage(pdfium.Handle, index);
                        if (page is null)
                        {
                            throw new InvalidOperationException($"Failed to load page {index} for rotation.");
                        }

                        try
                        {
                            var current = fpdf_edit.FPDFPageGetRotation(page);
                            fpdf_edit.FPDFPageSetRotation(page, (current + quarterTurns) % 4);
                        }
                        finally
                        {
                            fpdfview.FPDF_ClosePage(page);
                        }
                    }

                    pdfium.RebuildPages();
                }
            },
            cancellationToken);
    }

    public Task DeletePagesAsync(
        IPdfDocument document,
        IReadOnlyList<int> pageIndexes,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ValidateIndexes(pdfium, pageIndexes);
        if (pageIndexes.Count == 0)
        {
            return Task.CompletedTask;
        }

        if (pageIndexes.Distinct().Count() >= pdfium.PageCount)
        {
            throw new InvalidOperationException("Cannot delete every page from a PDF document.");
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    foreach (var index in pageIndexes.Distinct().OrderByDescending(i => i))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        fpdf_edit.FPDFPageDelete(pdfium.Handle, index);
                    }

                    pdfium.RebuildPages();
                }
            },
            cancellationToken);
    }

    public Task ReorderPagesAsync(
        IPdfDocument document,
        IReadOnlyList<int> newOrder,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ArgumentNullException.ThrowIfNull(newOrder);
        if (newOrder.Count != pdfium.PageCount)
        {
            throw new ArgumentException("newOrder must contain every page index exactly once.", nameof(newOrder));
        }

        if (newOrder.Distinct().Count() != newOrder.Count
            || newOrder.Any(i => i < 0 || i >= pdfium.PageCount))
        {
            throw new ArgumentException("newOrder must be a permutation of existing page indexes.", nameof(newOrder));
        }

        if (newOrder.Select((value, index) => value == index).All(x => x))
        {
            return Task.CompletedTask;
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var rebuilt = BuildDocumentFromPages(pdfium.Handle, newOrder);
                    pdfium.ReplaceHandle(rebuilt);
                }
            },
            cancellationToken);
    }

    public Task<IPdfDocument> ExtractPagesAsync(
        IPdfDocument document,
        IReadOnlyList<int> pageIndexes,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ValidateIndexes(pdfium, pageIndexes);
        if (pageIndexes.Count == 0)
        {
            throw new ArgumentException("At least one page index is required.", nameof(pageIndexes));
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var handle = BuildDocumentFromPages(pdfium.Handle, pageIndexes.ToArray());
                    var pages = new List<PdfiumPage>();
                    var extracted = new PdfiumDocument(path: null, handle, pages, isEncrypted: false);
                    pages.AddRange(PdfiumPageCatalog.Build(extracted, handle));
                    return (IPdfDocument)extracted;
                }
            },
            cancellationToken);
    }

    public Task InsertBlankPageAsync(
        IPdfDocument document,
        int insertIndex,
        double widthPoints = 612,
        double heightPoints = 792,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        if (insertIndex < 0 || insertIndex > pdfium.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(insertIndex));
        }

        if (widthPoints <= 0 || heightPoints <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(widthPoints), "Page size must be positive.");
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var page = fpdf_edit.FPDFPageNew(pdfium.Handle, insertIndex, widthPoints, heightPoints);
                    if (page is null)
                    {
                        throw new InvalidOperationException("Failed to insert a blank page.");
                    }

                    fpdfview.FPDF_ClosePage(page);
                    pdfium.RebuildPages();
                }
            },
            cancellationToken);
    }

    public Task DuplicatePagesAsync(
        IPdfDocument document,
        IReadOnlyList<int> pageIndexes,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ValidateIndexes(pdfium, pageIndexes);
        if (pageIndexes.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    // Copy via a temporary document — PDFium rejects same-handle ImportPages.
                    foreach (var index in pageIndexes.Distinct().OrderByDescending(i => i))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var temp = BuildDocumentFromPages(pdfium.Handle, [index]);
                        try
                        {
                            var ok = fpdf_ppo.FPDF_ImportPages(pdfium.Handle, temp, "1", index + 1);
                            if (ok == 0)
                            {
                                throw new InvalidOperationException($"Failed to duplicate page {index}.");
                            }
                        }
                        finally
                        {
                            fpdfview.FPDF_CloseDocument(temp);
                        }
                    }

                    pdfium.RebuildPages();
                }
            },
            cancellationToken);
    }

    public Task InsertPagesAsync(
        IPdfDocument document,
        IPdfDocument source,
        IReadOnlyList<int> sourcePageIndexes,
        int insertIndex,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        var sourceDoc = RequirePdfium(source);
        ValidateIndexes(sourceDoc, sourcePageIndexes);
        if (sourcePageIndexes.Count == 0)
        {
            return Task.CompletedTask;
        }

        if (insertIndex < 0 || insertIndex > pdfium.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(insertIndex));
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    sourceDoc.ThrowIfDisposed();
                    var range = PdfiumPageCatalog.ToPageRange(sourcePageIndexes);
                    var ok = fpdf_ppo.FPDF_ImportPages(pdfium.Handle, sourceDoc.Handle, range, insertIndex);
                    if (ok == 0)
                    {
                        throw new InvalidOperationException($"Failed to insert pages with range '{range}'.");
                    }

                    pdfium.RebuildPages();
                }
            },
            cancellationToken);
    }

    public async Task MergeDocumentsAsync(
        IPdfDocument document,
        IReadOnlyList<IPdfDocument> sources,
        int insertIndex,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var pdfium = RequirePdfium(document);
        if (insertIndex < 0 || insertIndex > pdfium.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(insertIndex));
        }

        var cursor = insertIndex;
        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(source);
            if (source.PageCount == 0)
            {
                continue;
            }

            var indexes = Enumerable.Range(0, source.PageCount).ToList();
            await InsertPagesAsync(document, source, indexes, cursor, cancellationToken);
            cursor += indexes.Count;
        }
    }

    public Task<IReadOnlyList<IPdfDocument>> SplitDocumentAsync(
        IPdfDocument document,
        IReadOnlyList<int> splitBeforeIndexes,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ArgumentNullException.ThrowIfNull(splitBeforeIndexes);
        var ranges = PdfSplitRanges.BuildRanges(pdfium.PageCount, splitBeforeIndexes);
        if (ranges.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<IPdfDocument>>([]);
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var parts = new List<IPdfDocument>(ranges.Count);
                    foreach (var range in ranges)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var handle = BuildDocumentFromPages(pdfium.Handle, range);
                        var pages = new List<PdfiumPage>();
                        var part = new PdfiumDocument(path: null, handle, pages, isEncrypted: false);
                        pages.AddRange(PdfiumPageCatalog.Build(part, handle));
                        parts.Add(part);
                    }

                    return (IReadOnlyList<IPdfDocument>)parts;
                }
            },
            cancellationToken);
    }

    public Task SaveAsync(IPdfDocument document, string path, CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    PdfiumDocumentSaver.SaveToPath(pdfium.Handle, path);
                    pdfium.Path = path;
                }
            },
            cancellationToken);
    }

    public Task<byte[]> SaveToBytesAsync(IPdfDocument document, CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    return PdfiumDocumentSaver.SaveToBytes(pdfium.Handle);
                }
            },
            cancellationToken);
    }

    public Task RestoreAsync(IPdfDocument document, byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ArgumentNullException.ThrowIfNull(pdfBytes);

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    pdfium.ReplaceFromBytes(pdfBytes);
                }
            },
            cancellationToken);
    }

    private static FpdfDocumentT BuildDocumentFromPages(FpdfDocumentT source, IReadOnlyList<int> zeroBasedOrder)
    {
        var dest = fpdf_edit.FPDF_CreateNewDocument();
        if (dest is null)
        {
            throw new InvalidOperationException("Failed to create a new PDF document.");
        }

        var range = PdfiumPageCatalog.ToPageRange(zeroBasedOrder);
        var ok = fpdf_ppo.FPDF_ImportPages(dest, source, range, 0);
        if (ok == 0)
        {
            fpdfview.FPDF_CloseDocument(dest);
            throw new InvalidOperationException($"Failed to import pages with range '{range}'.");
        }

        return dest;
    }

    private static PdfiumDocument RequirePdfium(IPdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document is not PdfiumDocument pdfium)
        {
            throw new ArgumentException("Document must be opened by PdfiumDocumentFactory.", nameof(document));
        }

        return pdfium;
    }

    private static void ValidateIndexes(PdfiumDocument document, IReadOnlyList<int> pageIndexes)
    {
        ArgumentNullException.ThrowIfNull(pageIndexes);
        foreach (var index in pageIndexes)
        {
            if (index < 0 || index >= document.PageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pageIndexes), index, "Page index out of range.");
            }
        }
    }
}
