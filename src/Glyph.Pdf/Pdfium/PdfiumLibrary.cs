using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

/// <summary>
/// Process-wide PDFium initialization guard.
/// </summary>
internal static class PdfiumLibrary
{
    private static readonly object Gate = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (Gate)
        {
            if (_initialized)
            {
                return;
            }

            fpdfview.FPDF_InitLibrary();
            _initialized = true;
        }
    }
}
