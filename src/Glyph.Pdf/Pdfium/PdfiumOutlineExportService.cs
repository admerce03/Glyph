using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Pdfium;

/// <summary>
/// Exports flat title/page bookmarks into the PDF outline tree (F09-08).
/// </summary>
public sealed class PdfiumOutlineExportService : IPdfOutlineExportService
{
    public Task ExportAsync(
        IPdfDocument document,
        IReadOnlyList<PdfOutlineExportEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(entries);
        if (document is not PdfiumDocument pdfium)
        {
            throw new ArgumentException("Document must be a PDFium document.", nameof(document));
        }

        if (entries.Count == 0)
        {
            throw new ArgumentException("At least one outline entry is required.", nameof(entries));
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var bytes = PdfiumDocumentSaver.SaveToBytes(pdfium.Handle, flags: 0);
                    var patched = PdfOutlinePatcher.Apply(
                        bytes,
                        entries.Select(e => new PdfOutlinePatcher.Entry(e.Title, e.PageIndex)).ToList());
                    pdfium.ReplaceFromBytes(patched);
                }
            },
            cancellationToken);
    }
}
