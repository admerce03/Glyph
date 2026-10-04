using Glyph.Pdf.Abstractions;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

internal sealed class PdfiumDocument : IPdfDocument
{
    private readonly IReadOnlyList<PdfiumPage> _pages;
    private bool _disposed;

    public PdfiumDocument(string? path, FpdfDocumentT handle, IReadOnlyList<PdfiumPage> pages, bool isEncrypted)
    {
        Path = path;
        Handle = handle;
        _pages = pages;
        IsEncrypted = isEncrypted;
    }

    public string? Path { get; }

    public int PageCount => _pages.Count;

    public bool IsEncrypted { get; }

    internal FpdfDocumentT Handle { get; }

    public IPdfPage GetPage(int pageIndex)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, _pages.Count);
        return _pages[pageIndex];
    }

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (PdfiumSync.Gate)
        {
            if (_disposed)
            {
                return;
            }

            fpdfview.FPDF_CloseDocument(Handle);
            _disposed = true;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
