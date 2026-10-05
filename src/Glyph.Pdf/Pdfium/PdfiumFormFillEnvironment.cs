using PDFiumCore;
using PDFiumCore.Delegates;

namespace Glyph.Pdf.Pdfium;

/// <summary>
/// Owns a PDFium form-fill environment for one document. Callbacks are retained
/// so the GC cannot collect them while the handle is live.
/// </summary>
internal sealed class PdfiumFormFillEnvironment : IDisposable
{
    // Keep delegates rooted for the lifetime of the unmanaged form handle.
#pragma warning disable IDE0052 // retained for native callbacks
    private readonly Action___IntPtr___IntPtr_double_double_double_double _invalidate;
    private readonly Action___IntPtr_int _setCursor;
#pragma warning restore IDE0052

    private readonly FPDF_FORMFILLINFO _info;
    private FpdfFormHandleT? _handle;
    private bool _disposed;

    public PdfiumFormFillEnvironment(FpdfDocumentT document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _invalidate = static (_, _, _, _, _, _) => { };
        _setCursor = static (_, _) => { };

        _info = new FPDF_FORMFILLINFO
        {
            Version = 2,
            FFI_Invalidate = _invalidate,
            FFI_SetCursor = _setCursor,
        };

        _handle = fpdf_formfill.FPDFDOC_InitFormFillEnvironment(document, _info)
            ?? throw new InvalidOperationException("FPDFDOC_InitFormFillEnvironment failed.");
    }

    public FpdfFormHandleT Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle!;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_handle is not null)
        {
            fpdf_formfill.FPDFDOC_ExitFormFillEnvironment(_handle);
            _handle = null;
        }

        // Keep _info alive until after Exit; then allow GC.
        GC.KeepAlive(_info);
        GC.KeepAlive(_invalidate);
        GC.KeepAlive(_setCursor);
    }
}
