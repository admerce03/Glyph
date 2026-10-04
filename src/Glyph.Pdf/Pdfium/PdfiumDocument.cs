using Glyph.Pdf.Abstractions;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

internal sealed class PdfiumDocument : IPdfDocument
{
    private List<PdfiumPage> _pages;
    private bool _disposed;

    public PdfiumDocument(string? path, FpdfDocumentT handle, List<PdfiumPage> pages, bool isEncrypted)
    {
        Path = path;
        Handle = handle;
        _pages = pages;
        IsEncrypted = isEncrypted;
    }

    public string? Path { get; set; }

    public int PageCount => _pages.Count;

    public bool IsEncrypted { get; }

    public event EventHandler? PagesChanged;

    internal FpdfDocumentT Handle { get; private set; }

    public IPdfPage GetPage(int pageIndex)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, _pages.Count);
        return _pages[pageIndex];
    }

    internal void RebuildPages()
    {
        ThrowIfDisposed();
        _pages = PdfiumPageCatalog.Build(this, Handle);
        PagesChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void ReplaceHandle(FpdfDocumentT newHandle)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(newHandle);

        var old = Handle;
        Handle = newHandle;
        if (!ReferenceEquals(old, newHandle))
        {
            fpdfview.FPDF_CloseDocument(old);
        }

        RebuildPages();
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
